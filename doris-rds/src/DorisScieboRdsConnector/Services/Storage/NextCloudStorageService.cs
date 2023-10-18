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

    private readonly string baseUrl;
    private readonly string webDavBaseUrl;

    private const string sha256ManifestFileName = "manifest-sha256.txt";
    private const string rootDirectoryName = "doris-datasets";

    public NextCloudStorageService(
        HttpClient httpClient,
        OcsApiClient ocsClient,
        ILogger<NextCloudStorageService> logger,
        IConfiguration configuration)
    {
        this.logger = logger;
        this.ocsClient = ocsClient;

        baseUrl = configuration.GetValue<string>("NextCloud:BaseUrl")!;
        string nextCloudUser = configuration.GetValue<string>("NextCloud:User")!;
        webDavBaseUrl = $"{baseUrl}/remote.php/dav/files/{nextCloudUser}";

        string authString = nextCloudUser + ":" + configuration.GetValue<string>("NextCloud:Password");
        string basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        httpClient.DefaultRequestHeaders.Add("Host", "localhost");

        webDavClient = new WebDavClient(httpClient);
    }

    public async Task SetupProject(string projectId)
    {
        if (await ProjectExists(projectId))
        {
            logger.LogInformation("📁SetupProject projectId exists: {projectId}", projectId);
            return;
        }

        var baseUri = GetWebDavBaseUri(projectId);

        logger.LogInformation("📁SetupProject create WebDav: {baseUri}", baseUri);

        await webDavClient.Mkcol(baseUri);
        //await webDavClient.Mkcol($"{webDavBaseUrl}/doris-datasets/{projectId}/data");

        // Ensure that manifest-sha256.txt exists so that we can update it later in AddFile
        await WriteSha256ManifestFile(baseUri, new MemoryStream());

        await GetOrCreateLinkShareToken(projectId);
    }

    public Task<bool> ProjectExists(string projectId)
    {
        return DirectoryExists(GetWebDavBaseUri(projectId));
    }

    public async Task AddFile(string projectId, string fileName, string contentType, Stream stream)
    {
        async Task EnsureDirectoryExists(Uri baseUri, Uri fileUri)
        {
            var directoriesToCreate = new Stack<string>();
            string uri = fileUri.AbsoluteUri;

            while ( (uri = uri[..uri.LastIndexOf('/')]) != baseUri.AbsoluteUri )
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

        async Task UpdateSha256File(Uri baseUri, byte[] sha256Hash)
        {
            static string PercentEncodePath(string path)
            {
                return path
                    .Replace("%", "%25")
                    .Replace("\n", "%0A")
                    .Replace("\r", "%0D");
            }

            async Task<IDictionary<string, string>> GetExistingValues(Uri baseUri)
            {
                // TODO assume that file exists, or create here if not found?
                var file = await webDavClient.GetRawFile(new Uri(baseUri, sha256ManifestFileName));

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

            var values = await GetExistingValues(baseUri);
            values[PercentEncodePath(fileName)] = Convert.ToHexString(sha256Hash);
            byte[] newContent = Encoding.UTF8.GetBytes(string.Join("\n", values.Select(k => k.Value + " " + k.Key)));

            await WriteSha256ManifestFile(baseUri, new MemoryStream(newContent));
        }

        Uri baseUri = GetWebDavBaseUri(projectId);
        var uploadUri = new Uri(baseUri, string.Join('/', fileName.Split('/').Select(Uri.EscapeDataString)));

        if (!baseUri.IsBaseOf(uploadUri))
        {
            throw new ArgumentException(nameof(fileName), "Illegal file name.");
        }

        // TODO Error handling. When do we need to abort etc?

        logger.LogDebug("AddFile uploadUri 🐛 {uploadUri}", uploadUri);
        logger.LogDebug("AddFile contentType 🐛 {contentType}", contentType);

        await EnsureDirectoryExists(baseUri, uploadUri);

        using var sha256 = SHA256.Create();
        var hashStream = new HashStream(stream, sha256);

        var result = await webDavClient.PutFile(uploadUri, stream, contentType);

        await UpdateSha256File(baseUri, hashStream.Hash()!);

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
        //TODO: private/public should be handled in some way...
        var fileList = new List<RoFile>();
        var uri = GetWebDavBaseUri(projectId);

        string shareToken = await GetOrCreateLinkShareToken(projectId);
        logger.LogInformation("📁GetFiles projectId: {projectId} shareToken: {shareToken}", projectId, shareToken);

        var result = await webDavClient.Propfind(uri, new() { ApplyTo = ApplyTo.Propfind.ResourceAndAncestors });

        if (result.IsSuccessful)
        {
            foreach (var res in result.Resources)
            {
                if (res.IsCollection)
                {
                    logger.LogDebug("📁 directory {dirUri}", res.Uri);
                    continue;
                }

                // get the relative path from the dataset directory
                string filePath = res.Uri.Split(uri.PathAndQuery)[1].TrimStart('/');

                string fileName = filePath.Split('/')[^1];
                string dirPath = string.Join('/', filePath.Split('/')[..^1]);

                fileList.Add(new(
                    Id: filePath,
                    ContentSize: res.ContentLength.ToString(),
                    EncodingFormat: res.ContentType,
                    DateModified: res.LastModifiedDate,
                    Url: new Uri($"{baseUrl}/s/{shareToken}/download?path=%2F{dirPath}&files={fileName}")
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

    private Uri GetWebDavBaseUri(string projectId) =>
         new($"{webDavBaseUrl}/{Uri.EscapeDataString(rootDirectoryName)}/{Uri.EscapeDataString(projectId)}/", UriKind.Absolute);
   
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

    private Task WriteSha256ManifestFile(Uri baseUri, Stream data)
    {
        return webDavClient.PutFile(new Uri(baseUri, sha256ManifestFileName), data, "text/plain");
    }

    public async Task<string> GetOrCreateLinkShareToken(string projectId)
    {
        OcsShare? share = await GetOcsShare(projectId);

        if (share is null)
        {
            share = await CreateOcsShare(projectId);
        }

        if (share?.token is not null)
        {
            return share.token;
        }

        logger.LogError($"GetOcsShareToken ERROR for {projectId}");
        return "NO-TOKEN";
    }

    private async Task<OcsShare?> GetOcsShare(string projectId)
    {
        //TODO: public/private handling
        var ocsResponse = await ocsClient.GetShares(new() 
        { 
            path = GetLinkSharePath(projectId)
        });

        foreach (var share in ocsResponse.ocs.data)
        {
            if (share.label == "dataset-share")
            {
                return share;
            }
        }

        return null;
    }

    private async Task<OcsShare> CreateOcsShare(string projectId)
    {
        var ocsResponse = await ocsClient.CreateShare(new()
        {
            path = GetLinkSharePath(projectId),
            permissions = 1,
            shareType = 3,
            publicUpload = false,
            label = "dataset-share"
        });

        return ocsResponse.ocs.data;
    }

    private static string GetLinkSharePath(string projectId) => $"{rootDirectoryName}/{projectId}";
}
