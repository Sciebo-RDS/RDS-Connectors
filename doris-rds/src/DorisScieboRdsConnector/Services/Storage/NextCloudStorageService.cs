namespace DorisScieboRdsConnector.Services.Storage;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using WebDav;
using DorisScieboRdsConnector.Models;

public class NextCloudStorageService : IStorageService
{
    private readonly ILogger<NextCloudStorageService> logger;
    private IWebDavClient webDav;
    private HttpClient httpClient;
    private IConfiguration configuration;

    private string? baseUrl;
    private string? nextCloudUser;
    private string? webDavBaseUrl;

    public NextCloudStorageService(IWebDavClient webDav, HttpClient httpClient, ILogger<NextCloudStorageService> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.webDav = webDav;
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.baseUrl = configuration.GetValue<string>("NextCloud:BaseUrl");
        this.nextCloudUser = configuration.GetValue<string>("NextCloud:User");
        this.webDavBaseUrl = $"{baseUrl}/remote.php/dav/files/{nextCloudUser}";
    }
    public async Task AddFile(string projectId, string fileName, string contentType, Stream stream)
    {
        string fileUploadUrl = $"{webDavBaseUrl}/doris-datasets/{projectId}/data/{fileName}";
        
        logger.LogDebug($"AddFile fileUploadUrl 🐛 {fileUploadUrl}");
        logger.LogDebug($"AddFile contentType 🐛 {contentType}");
        
        var result = await webDav.PutFile(fileUploadUrl, stream, contentType);
        
        if(result.IsSuccessful){
            logger.LogDebug($"AddFile OK 🐛 {fileUploadUrl}");
            logger.LogInformation($"AddFile OK {fileUploadUrl}");
        }else{
            logger.LogError($"AddFile UPLOAD FAIL {fileUploadUrl}");
            logger.LogInformation($"AddFile FAILED WebDav Response {result}");
        }
    }

    public async Task<bool> ProjectExist(string projectId){
        logger.LogInformation($"ProjectExist PROPFIND {webDavBaseUrl}/doris-datasets/{projectId}");
        var result = await webDav.Propfind($"{webDavBaseUrl}/doris-datasets/{projectId}");
        if(result.IsSuccessful == false){
            throw new Exception($"ERROR checking for webdav dir {webDavBaseUrl}/doris-datasets/{projectId}");
        }
  
        foreach (var res in result.Resources)
        {
            logger.LogInformation($"ProjectExist res {res.Uri}");
            if(res.IsCollection){
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
        logger.LogInformation($"📁GetFiles projectId: {projectId} shareToken: {shareToken}");

        var propfindParameters = new PropfindParameters{ ApplyTo = ApplyTo.Propfind.ResourceAndAncestors };
        var result = await webDav.Propfind(url, propfindParameters);

        if (result.IsSuccessful)
        {
            foreach (var res in result.Resources)
            {
                if(res.IsCollection){
                    logger.LogDebug($"📁 directory {res.Uri}");
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
        }else{
            logger.LogError($"GetFiles ERROR listing files from WebDAV {projectId}");
            logger.LogError(result.ToString());
        }

        return fileList;
    }

    public async Task SetupProject(string projectId)
    {
        logger.LogInformation($"📁 IN SetupProject projectId: {projectId}");
        if(await ProjectExist(projectId)){
            logger.LogInformation($"📁SetupProject projectId exists: {projectId}");
            return;
        }

        logger.LogInformation($"📁SetupProject create WebDav: {webDavBaseUrl}/doris-datasets/{projectId}");
        await webDav.Mkcol($"{webDavBaseUrl}/doris-datasets/{projectId}");
        await webDav.Mkcol($"{webDavBaseUrl}/doris-datasets/{projectId}/data");
        await GetOcsShareToken(projectId);
    }

    public async Task<string> GetOcsShareToken(string projectId){
		OcsShare? share = await GetOcsShare(projectId);
        
        if(share is null){
            share = await CreateOcsShare(projectId);
        }
        
        if(share?.token is not null){
            return share.token;
        }

        logger.LogError($"GetOcsShareToken ERROR for {projectId}");
        return "NO-TOKEN";
    }

    private async Task<OcsShare?> GetOcsShare(string projectId){
        //TODO: public/private handling
        Uri shareApiUri = new Uri($"{baseUrl}/ocs/v2.php/apps/files_sharing/api/v1/shares?path=doris-datasets/{projectId}");

		using (var request = new HttpRequestMessage(HttpMethod.Get, shareApiUri))
		{
			request.Headers.Add("OCS-APIRequest", "true");
			request.Headers.Add("Accept", "application/json");
            var response = await httpClient.SendAsync(request);

			response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync();
            logger.LogDebug($"🌐 GetOcsShare response: {responseString}");
            OcsGetResponse ocsResponse = JsonSerializer.Deserialize<OcsGetResponse>(responseString)!;
            
            List<OcsShare> shares = ocsResponse?.ocs?.data ?? new List<OcsShare>();

            foreach(var share in shares){
                if(share.label == "dataset-share"){
                    return share;
                }
            }
        }
        
        return null;
    }

    private async Task<OcsShare?> CreateOcsShare(string projectId){
        Uri shareApiUri = new Uri($"{baseUrl}/ocs/v2.php/apps/files_sharing/api/v1/shares");

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
            
            if(ocsResponse?.ocs?.data is not null){
                return ocsResponse?.ocs?.data;
            }
        }
        return null;
    }

}
