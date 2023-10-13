namespace DorisScieboRdsConnector.Services.Storage;

using DorisScieboRdsConnector.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using WebDav;

public class NextCloudStorageService : IStorageService
{
    private readonly ILogger logger;
    private readonly IWebDavClient webDavClient;
    private readonly OcsApiClient ocsClient;

    private readonly string baseUrl;
    private readonly string webDavBaseUrl;

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
        string basicAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authString));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

        webDavClient = new WebDavClient(httpClient);
    }

    public async Task AddFile(string projectId, string fileName, string contentType, Stream stream)
    {
        string fileUploadUrl = $"{webDavBaseUrl}/doris-datasets/{projectId}/data/{fileName}";

        logger.LogDebug("AddFile fileUploadUrl 🐛 {fileUploadUrl}", fileUploadUrl);
        logger.LogDebug("AddFile contentType 🐛 {contentType}", contentType);

        var result = await webDavClient.PutFile(fileUploadUrl, stream, contentType);

        if (result.IsSuccessful)
        {
            logger.LogDebug("AddFile OK 🐛 {fileUploadUrl}", fileUploadUrl);
            logger.LogInformation("AddFile OK {fileUploadUrl}", fileUploadUrl);
        }
        else
        {
            logger.LogError("AddFile UPLOAD FAIL {fileUploadUrl}", fileUploadUrl);
            logger.LogInformation("AddFile FAILED WebDav Response {result}", result);
        }
    }

    public async Task<bool> ProjectExists(string projectId)
    {
        logger.LogInformation("ProjectExists PROPFIND {webDavBaseUrl}/doris-datasets/{projectId}", webDavBaseUrl, projectId);

        var result = await webDavClient.Propfind($"{webDavBaseUrl}/doris-datasets/{projectId}");

        foreach (var res in result.Resources)
        {
            logger.LogInformation("ProjectExists res {resUri}", res.Uri);
            if (res.IsCollection)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<IEnumerable<RoFile>> GetFiles(string projectId)
    {
        //TODO: private/public should be handled in some way...
        var url = $"{webDavBaseUrl}/doris-datasets/{projectId}";
        var fileList = new List<RoFile>();
        var uri = new Uri(url);

        string shareToken = await GetOcsShareToken(projectId);
        logger.LogInformation("📁GetFiles projectId: {projectId} shareToken: {shareToken}", projectId, shareToken);

        var result = await webDavClient.Propfind(url, new() { ApplyTo = ApplyTo.Propfind.ResourceAndAncestors });

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

    public async Task SetupProject(string projectId)
    {
        if (await ProjectExists(projectId))
        {
            logger.LogInformation("📁SetupProject projectId exists: {projectId}", projectId);
            return;
        }

        logger.LogInformation("📁SetupProject create WebDav: {webDavBaseUrl}/doris-datasets/{projectId}", webDavBaseUrl, projectId);
        await webDavClient.Mkcol($"{webDavBaseUrl}/doris-datasets/{projectId}");
        await webDavClient.Mkcol($"{webDavBaseUrl}/doris-datasets/{projectId}/data");
        await GetOcsShareToken(projectId);
    }

    public async Task<string> GetOcsShareToken(string projectId)
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
        var ocsResponse = await ocsClient.GetShares(new() { path = $"doris-datasets/{projectId}" });

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
            path = $"doris-datasets/{projectId}",
            permissions = 1,
            shareType = 3,
            publicUpload = false,
            label = "dataset-share"
        });

        return ocsResponse.ocs.data;
    }
}
