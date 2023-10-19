namespace DorisScieboRdsConnector.Services.Storage;

using DorisScieboRdsConnector.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WebDav;

public class NextCloudStorageService : IStorageService
{
    private readonly ILogger logger;
    private readonly IWebDavClient webDavClient;
    private readonly OcsApiClient ocsClient;

    private readonly Uri webDavBaseUri;

    private const string rootDirectoryName = "doris-datasets";
    private const string linkShareLabel = "dataset-share";

    public NextCloudStorageService(
        HttpClient httpClient,
        OcsApiClient ocsClient,
        ILogger<NextCloudStorageService> logger,
        IConfiguration configuration)
    {
        this.logger = logger;
        this.ocsClient = ocsClient;

        var baseUri = new Uri(configuration.GetValue<string>("NextCloud:BaseUrl")!);
        string nextCloudUser = configuration.GetValue<string>("NextCloud:User")!;
        webDavBaseUri = new Uri(baseUri, $"remote.php/dav/files/{Uri.EscapeDataString(nextCloudUser)}/");

        string authString = nextCloudUser + ":" + configuration.GetValue<string>("NextCloud:Password");
        string basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        httpClient.DefaultRequestHeaders.Add("Host", "localhost");

        webDavClient = new WebDavClient(httpClient);
    }

    public async Task SetupProject(string projectId)
    {
        var baseUri = GetProjectWebDavUri(projectId);

        if (await DirectoryExists(baseUri))
        {
            logger.LogInformation("📁SetupProject projectId exists: {projectId}", projectId);
        }
        else
        {
            logger.LogInformation("📁SetupProject create WebDav: {baseUri}", baseUri);
            await webDavClient.Mkcol(baseUri);
        }
       
        await GetOrCreateLinkShare(projectId);
    }

    public Task<bool> ProjectExists(string projectId)
    {
        return DirectoryExists(GetProjectWebDavUri(projectId));
    }

    public async Task AddFile(string projectId, string fileName, RoFileType type, string contentType, Stream stream)
    {
        async Task EnsureDirectoryExists(Uri baseUri, Uri uploadUri)
        {
            var directoriesToCreate = new Stack<string>();
            string uri = uploadUri.AbsoluteUri;

            while (!baseUri.AbsoluteUri.Equals(uri = uri[..uri.LastIndexOf('/')]))
            {
                if (await DirectoryExists(new Uri(uri)))
                {
                    break;
                }

                directoriesToCreate.Push(uri);
            }

            foreach (var directoryUri in directoriesToCreate)
            {
                await webDavClient.Mkcol(directoryUri);
            }
        }

        async Task UpdateSha256File(Uri baseUri, string filePath, byte[] sha256Hash)
        {
            static string PercentEncodePath(string path)
            {
                return path
                    .Replace("%", "%25")
                    .Replace("\n", "%0A")
                    .Replace("\r", "%0D");
            }

            async Task<IDictionary<string, string>> GetExistingValues(Uri fileUri)
            {
                var file = await webDavClient.GetRawFile(fileUri);

                if (file.StatusCode == 404)
                {
                    return new Dictionary<string, string>();
                }

                using var reader = new StreamReader(file.Stream, Encoding.UTF8);
                var result = new Dictionary<string, string>();

                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    int index = line.IndexOf(' ');
                    string hash = line[..index];
                    string fileName = line[(index + 1)..];

                    result[fileName] = hash;
                }

                return result;
            }

            var fileUri = new Uri(baseUri, "manifest-sha256.txt");
            var values = await GetExistingValues(fileUri);
            values[PercentEncodePath(filePath)] = Convert.ToHexString(sha256Hash);
            var newContent = Encoding.UTF8.GetBytes(string.Join("\n", values.Select(k => k.Value + " " + k.Key)));

            await webDavClient.PutFile(fileUri, new MemoryStream(newContent), "text/plain");
        }

        Uri baseUri = GetProjectWebDavUri(projectId);
        string filePath = type.ToString() + "/" + fileName;
        var uploadUri = new Uri(baseUri,
            // To generate a valid URI, we must encode each part of the file path
            string.Join('/', filePath.Split('/').Select(Uri.EscapeDataString)));

        if (!baseUri.IsBaseOf(uploadUri))
        {
            throw new ArgumentException("Illegal file name.", nameof(fileName));
        }

        // TODO Error handling. When do we need to abort etc?

        logger.LogDebug("AddFile uploadUri 🐛 {uploadUri}", uploadUri);
        logger.LogDebug("AddFile contentType 🐛 {contentType}", contentType);

        await EnsureDirectoryExists(baseUri, uploadUri);

        using var sha256 = SHA256.Create();
        var hashStream = new HashStream(stream, sha256);

        var result = await webDavClient.PutFile(uploadUri, stream, contentType);

        await UpdateSha256File(baseUri, filePath, hashStream.Hash()!);

        if (result.IsSuccessful)
        {
            logger.LogDebug("AddFile OK 🐛 {fileUploadUrl}", uploadUri);
            logger.LogInformation("AddFile OK {fileUploadUrl}", uploadUri);
        }
        else
        {
            logger.LogError("AddFile UPLOAD FAIL {fileUploadUrl}", uploadUri);
            logger.LogInformation("AddFile FAILED WebDav Response {result}", result);
        }
    }

    public async Task<IEnumerable<RoFile>> GetFiles(string projectId)
    {
        var fileList = new List<RoFile>();
        var baseUri = GetProjectWebDavUri(projectId);

        string shareToken = (await GetOrCreateLinkShare(projectId)).token!;
        logger.LogInformation("📁GetFiles projectId: {projectId} shareToken: {shareToken}", projectId, shareToken);

        var result = await webDavClient.Propfind(baseUri, new()
        {
            ApplyTo = ApplyTo.Propfind.ResourceAndAncestors
        });

        if (result.IsSuccessful)
        {
            foreach (var res in result.Resources.Where(r => !r.IsCollection))
            {
                // Get the relative path from the project directory
                string filePath = baseUri.MakeRelativeUri(new Uri(baseUri, res.Uri)).ToString();

                RoFileType type;
                if (filePath.StartsWith("data/"))
                {
                    type = RoFileType.data;
                }
                else if (filePath.StartsWith("documentation/"))
                {
                    type = RoFileType.documentation;
                }
                else
                {
                    // Do not include files outside of data and documentation directories
                    continue;
                }

                int slashIndex = filePath.LastIndexOf('/');
                string dirPath = filePath[..slashIndex];
                string fileName = filePath[(slashIndex + 1)..];

                fileList.Add(new(
                    Id: filePath,
                    Type: type,
                    ContentSize: res.ContentLength.GetValueOrDefault(),
                    EncodingFormat: res.ContentType,
                    DateModified: res.LastModifiedDate?.ToUniversalTime(),
                    Url: new Uri(baseUri, $"/s/{shareToken}/download?path={Uri.EscapeDataString(dirPath)}&files={Uri.EscapeDataString(fileName)}")
                ));
            }
        }
        else
        {
            logger.LogError("GetFiles ERROR listing files from WebDAV {projectId}", projectId);
            logger.LogError("{result}", result);
        }

        return fileList;
    }

    private Uri GetProjectWebDavUri(string projectId) =>
         new(webDavBaseUri, $"{Uri.EscapeDataString(rootDirectoryName)}/{Uri.EscapeDataString(projectId)}/");

    private async Task<bool> DirectoryExists(Uri uri)
    {
        var result = await webDavClient.Propfind(uri, new()
        {
            ApplyTo = ApplyTo.Propfind.ResourceOnly,
        });

        return
            result.IsSuccessful &&
            result.Resources.Count == 1 &&
            result.Resources.First().IsCollection;
    }

    public async Task<OcsShare> GetOrCreateLinkShare(string projectId)
    {
        return await GetLinkShare(projectId) ?? await CreateLinkShare(projectId);
    }

    private async Task<OcsShare?> GetLinkShare(string projectId)
    {
        var response = await ocsClient.GetShares(new()
        {
            path = GetLinkSharePath(projectId)
        });

        return response.ocs.data.FirstOrDefault(share => share.label == linkShareLabel);
    }

    private async Task<OcsShare> CreateLinkShare(string projectId)
    {
        var ocsResponse = await ocsClient.CreateShare(new()
        {
            path = GetLinkSharePath(projectId),
            permissions = 1, // read
            shareType = 3, // public link
            publicUpload = false,
            label = linkShareLabel,
            // Using RandomNumberGenerator to generate a cryptographically secure random string
            password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
        });

        return ocsResponse.ocs.data;
    }

    private static string GetLinkSharePath(string projectId) => $"{rootDirectoryName}/{projectId}";
}
