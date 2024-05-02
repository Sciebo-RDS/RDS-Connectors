namespace DorisScieboRdsConnector.Services.Storage.NextCloud;

using DorisScieboRdsConnector.Configuration;
using DorisScieboRdsConnector.RoCrate;
using DorisScieboRdsConnector.Services.Storage;
using DorisScieboRdsConnector.Services.Storage.NextCloud.OcsApi;
using DorisScieboRdsConnector.Services.Storage.NextCloud.OcsApi.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nerdbank.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WebDav;

public class NextCloudStorageService : IStorageService
{
    private readonly ILogger logger;
    private readonly WebDavClient webDavClient;
    private readonly OcsApiClient ocsClient;
    private readonly NextCloudConfiguration configuration;

    private readonly Uri webDavFilesBaseUri;
    private readonly Uri webDavUploadsBaseUri;

    private const string rootDirectoryName = "doris-datasets";
    private const string linkShareLabel = "dataset-share";
    private const int uploadChunkSize = 10 * 1024 * 1024;

    private const string roCrateFileName = "ro-crate-metadata.json";

    public NextCloudStorageService(
        HttpClient httpClient,
        OcsApiClient ocsClient,
        ILogger<NextCloudStorageService> logger,
        IOptions<NextCloudConfiguration> configuration)
    {
        this.logger = logger;
        this.ocsClient = ocsClient;
        this.configuration = configuration.Value;

        httpClient.SetupForNextCloud(this.configuration);

        webDavFilesBaseUri = new Uri(this.configuration.BaseUrl, $"remote.php/dav/files/{Uri.EscapeDataString(this.configuration.User)}/");
        webDavUploadsBaseUri = new Uri(this.configuration.BaseUrl, $"remote.php/dav/uploads/{Uri.EscapeDataString(this.configuration.User)}/");
        webDavClient = new WebDavClient(httpClient);
    }

    public async Task SetupProject(string projectId)
    {
        async Task CreateDirectory(Uri uri)
        {
            if (await DirectoryExists(uri))
            {
                logger.LogDebug("📁SetupProject directory exists: {uri}", uri);
            }
            else
            {
                logger.LogDebug("📁SetupProject create directory: {uri}", uri);
                await webDavClient.Mkcol(uri);
            }
        }

        var baseUri = GetProjectWebDavUri(projectId);

        await CreateDirectory(baseUri);
        await CreateDirectory(new Uri(baseUri, "data/"));
        await CreateDirectory(new Uri(baseUri, "data/data/"));

        await GetOrCreateLinkShare(projectId);
    }

    public Task<bool> ProjectExists(string projectId)
    {
        return DirectoryExists(GetProjectWebDavUri(projectId));
    }

    public Task<string?> GetDataReviewLink(string projectId)
    {
        string url = new Uri(configuration.BaseUrl,
            $"apps/files?dir=" + Uri.EscapeDataString(rootDirectoryName + '/' + projectId)).AbsoluteUri;

        return Task.FromResult<string?>(url);
    }

    public async Task StoreRoCrateMetadata(string projectId, string metadata)
    {
        var roCrateUri = new Uri(GetProjectWebDavUri(projectId), roCrateFileName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(metadata));
        await webDavClient.PutFile(roCrateUri, stream, "application/ld+json");
    }

    public async Task<string?> GetRoCrateMetadata(string projectId)
    {
        var roCrateUri = new Uri(GetProjectWebDavUri(projectId), roCrateFileName);
        var file = await webDavClient.GetRawFile(roCrateUri);

        if (file.StatusCode == 404)
        {
            return null;
        }

        using var reader = new StreamReader(file.Stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    public async Task AddFile(string projectId, string fileName, Stream stream)
    {
        async Task EnsureDirectoryExists(Uri baseUri, Uri destinationUri)
        {
            var directoriesToCreate = new Stack<string>();
            string uri = destinationUri.AbsoluteUri;

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

        // Upload via NextClouds WebDav chunked upload API
        async Task Upload(Uri destinationUri, Stream stream)
        {
            var uri = new Uri(webDavUploadsBaseUri, "doris-connector-upload-" + Guid.NewGuid().ToString() + "/");
            // Add Destination header to all webdav calls to ensure we use v2 of the WebDav chunked upload API
            var destinationHeader = new KeyValuePair<string, string>[] { new("Destination", destinationUri.AbsoluteUri) };

            await webDavClient.Mkcol(uri, new()
            {
                Headers = destinationHeader
            });

            long bytesRead = 0;
            var monitoringStream = new MonitoringStream(stream);
            monitoringStream.DidRead += (_, e) =>
            {
                bytesRead += e.Count;
            };

            int chunk = 1;
            while (bytesRead % uploadChunkSize == 0)
            {
                await webDavClient.PutFile(new Uri(uri, chunk.ToString()), monitoringStream.ReadSlice(uploadChunkSize), new PutFileParameters
                {
                    Headers = destinationHeader
                });

                chunk++;
            }

            // Destination header is already included in Move by default
            await webDavClient.Move(new Uri(uri, ".file"), destinationUri);
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

            var values = await GetSha256ManifestValues(baseUri);
            values[PercentEncodePath(filePath)] = Convert.ToHexString(sha256Hash).ToLower();
            var newContent = Encoding.UTF8.GetBytes(string.Join("\n", values.Select(k => k.Value + " " + k.Key)));

            await webDavClient.PutFile(GetSha256ManifestUri(baseUri), new MemoryStream(newContent), "text/plain");
        }

        Uri baseUri = GetProjectWebDavUri(projectId);
        string filePath = "data/data/" + fileName;
        var destinationUri = new Uri(baseUri,
            // To generate a valid URI, we must encode each part of the file path
            string.Join('/', filePath.Split('/').Select(Uri.EscapeDataString)));

        if (!baseUri.IsBaseOf(destinationUri))
        {
            throw new ArgumentException("Illegal file name.", nameof(fileName));
        }

        // TODO Error handling. When do we need to abort etc?

        logger.LogDebug("AddFile destinationUri 🐛 {destinationUri}", destinationUri);

        await EnsureDirectoryExists(baseUri, destinationUri);

        using var sha256 = SHA256.Create();
        using var hashStream = new CryptoStream(stream, sha256, CryptoStreamMode.Read);

        await Upload(destinationUri, hashStream);

        await UpdateSha256File(baseUri, filePath, sha256.Hash!);
    }

    public async Task<IEnumerable<RoFile>> GetFiles(string projectId)
    {
        var fileList = new List<RoFile>();
        var baseUri = GetProjectWebDavUri(projectId);
        var linkShareUri = new Uri(baseUri, "data/data/");

        string shareToken = (await GetOrCreateLinkShare(projectId)).token!;
        logger.LogDebug("📁GetFiles projectId: {projectId} shareToken: {shareToken}", projectId, shareToken);

        var sha256Lookup = await GetSha256ManifestValues(baseUri);

        var result = await webDavClient.Propfind(linkShareUri, new()
        {
            ApplyTo = ApplyTo.Propfind.ResourceAndAncestors
        });

        if (result.IsSuccessful)
        {
            foreach (var res in result.Resources.Where(r => !r.IsCollection))
            {
                var resultUri = new Uri(baseUri, res.Uri);
                // Relative path from project directory
                string id = baseUri.MakeRelativeUri(resultUri).ToString();
                // Relative path from link share directory
                string relativePath = linkShareUri.MakeRelativeUri(resultUri).ToString();
                // Calculate path and fileName from relativePath
                int slashIndex = relativePath.LastIndexOf('/');
                string path = slashIndex >= 0 ? relativePath[..slashIndex] : "/";
                string fileName = relativePath[(slashIndex + 1)..];

                fileList.Add(new(
                    Id: id,
                    ContentSize: res.ContentLength.GetValueOrDefault(),
                    DateModified: res.LastModifiedDate?.ToUniversalTime(),
                    EncodingFormat: res.ContentType,
                    Sha256: sha256Lookup.TryGetValue(id, out string? value) ? value : null,
                    Url: new Uri(baseUri, $"/s/{shareToken}/download?path={Uri.EscapeDataString(path)}&files={Uri.EscapeDataString(fileName)}")
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
         new(webDavFilesBaseUri, $"{Uri.EscapeDataString(rootDirectoryName)}/{Uri.EscapeDataString(projectId)}/");

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

    private async Task<IDictionary<string, string>> GetSha256ManifestValues(Uri baseUri)
    {
        var result = new Dictionary<string, string>();

        var file = await webDavClient.GetRawFile(GetSha256ManifestUri(baseUri));

        if (file.StatusCode == 404)
        {
            return result;
        }

        using var reader = new StreamReader(file.Stream, Encoding.UTF8);
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

    private static Uri GetSha256ManifestUri(Uri baseUri) => new(baseUri, "manifest-sha256.txt");

    private async Task<OcsShare> GetOrCreateLinkShare(string projectId)
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
        var response = await ocsClient.CreateShare(new()
        {
            path = GetLinkSharePath(projectId),
            permissions = 1, // read
            shareType = 3, // public link
            publicUpload = false,
            label = linkShareLabel,
            // Using RandomNumberGenerator to generate a cryptographically secure random string
            password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
        });

        return response.ocs.data;
    }

    private static string GetLinkSharePath(string projectId) => $"{rootDirectoryName}/{projectId}/data/data";
}
