namespace DorisScieboRdsConnector.Services.Storage;

using DorisScieboRdsConnector.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using WebDav;

public class NextCloudStorageService : IStorageService
{
    private readonly ILogger logger;
    private readonly IWebDavClient webDavClient;
    private readonly HttpClient httpClient;

    private readonly string baseUrl;
    private readonly string webDavBaseUrl;

    public NextCloudStorageService(
        HttpClient httpClient, 
        ILogger<NextCloudStorageService> logger, 
        IConfiguration configuration)
    {
        this.logger = logger;
        this.httpClient = httpClient;

        baseUrl = configuration.GetValue<string>("NextCloud:BaseUrl")!;
        string nextCloudUser = configuration.GetValue<string>("NextCloud:User")!;
        webDavBaseUrl = $"{baseUrl}/remote.php/dav/files/{nextCloudUser}";

        string authString = nextCloudUser + ":" + configuration.GetValue<string>("NextCloud:Password");
        string basicAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authString));

        this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        //this.httpClient.DefaultRequestHeaders.Add("Host", "localhost"); // TODO is this needed?

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
        var uri  = new Uri(url);

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
                string dirPath = String.Join('/', filePath.Split('/')[..^1]);
                
                fileList.Add(new(
                    Id : filePath,
                    ContentSize : res.ContentLength.ToString(),
                    EncodingFormat : res.ContentType,
                    DateModified : res.LastModifiedDate,
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

    private async Task<OcsShare?> GetOcsShare(string projectId){
        //TODO: public/private handling
        var shareApiUri = new Uri($"{baseUrl}/ocs/v2.php/apps/files_sharing/api/v1/shares?path=doris-datasets/{projectId}");

		using (var request = new HttpRequestMessage(HttpMethod.Get, shareApiUri))
		{
			request.Headers.Add("OCS-APIRequest", "true");
			request.Headers.Add("Accept", "application/json");
            var response = await httpClient.SendAsync(request);

			response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync();
            logger.LogDebug("🌐 GetOcsShare response: {responseString}", responseString);
            OcsGetResponse ocsResponse = JsonSerializer.Deserialize<OcsGetResponse>(responseString)!;
            
            List<OcsShare> shares = ocsResponse?.ocs?.data ?? new List<OcsShare>();

            foreach (var share in shares)
            {
                if (share.label == "dataset-share")
                {
                    return share;
                }
            }
        }
        
        return null;
    }

    private async Task<OcsShare?> CreateOcsShare(string projectId){
        var shareApiUri = new Uri($"{baseUrl}/ocs/v2.php/apps/files_sharing/api/v1/shares");

        var values = new Dictionary<string, string>
        {
            { "path", $"doris-datasets/{projectId}" }, //TODO: public/private handling
            { "permissions", "1" },
            { "shareType", "3" },
            { "publicUpload", "false" },
            { "label", "dataset-share" }
        };

		using (var request = new HttpRequestMessage(HttpMethod.Post, shareApiUri))
		{
			request.Headers.Add("OCS-APIRequest", "true");
			request.Headers.Add("Accept", "application/json");
            request.Content = new FormUrlEncodedContent(values);
            var response = await httpClient.SendAsync(request);

			response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync();
            logger.LogDebug($"🌐 CreateOcsShare response: {responseString}");
            OcsPostResponse ocsResponse = JsonSerializer.Deserialize<OcsPostResponse>(responseString)!;
            
            if (ocsResponse?.ocs?.data is not null)
            {
                return ocsResponse?.ocs?.data;
            }
        }
        return null;
    }

}
