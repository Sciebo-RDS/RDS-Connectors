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
using Minio;
using System.Text.Json.Nodes;
using DorisScieboRdsConnector.Models;
using System.Reflection;

public class NextCloudStorageService : IStorageService
{
    private readonly ILogger logger;
    private IWebDavClient webDav;
    private HttpClient httpClient;
    private IConfiguration configuration;

    private string? baseUrl;
    private string? nextCloudUser;
    private string? webDavBaseUrl;

    public NextCloudStorageService(IWebDavClient webDav, HttpClient httpClient, ILogger logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.webDav = webDav;
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.baseUrl = configuration.GetValue<string>("NextCloud:BaseUrl");
        this.nextCloudUser = configuration.GetValue<string>("NextCloud:User");
        this.webDavBaseUrl = $"{this.baseUrl}/remote.php/dav/files/{this.nextCloudUser}";
    }
    public async Task AddFile(string projectId, string fileName, string contentType, Stream stream)
    {
        string fileUploadUrl = $"{this.webDavBaseUrl}/public-datasets/{projectId}/data/{fileName}";
        
        this.logger.LogDebug($"AddFile fileUploadUrl 🐛 {fileUploadUrl}");
        this.logger.LogDebug($"AddFile contentType 🐛 {contentType}");
        
        var result = await this.webDav.PutFile(fileUploadUrl, stream, contentType);
        
        if(result.IsSuccessful){
            this.logger.LogDebug($"AddFile OK 🐛 {fileUploadUrl}");
        }else{
            this.logger.LogError($"AddFile UPLOAD FAIL 🐛 {fileUploadUrl}");
            this.logger.LogInformation(result.ToString());
        }
    }

    public async Task<bool> ProjectExist(string projectId){
        var result = await this.webDav.Propfind($"{this.webDavBaseUrl}/public-datasets/{projectId}");
        if(result.IsSuccessful == false){
            return false;
        }

        foreach (var res in result.Resources)
        {
            if(res.IsCollection){
                return true;
            }
        }

        return false;
    }

    public async Task<IEnumerable<Models.File>> GetFiles(string projectId)
    {   
        //TODO: private/public should be handled in some way...
        var url = $"{this.webDavBaseUrl}/public-datasets/{projectId}";
        var fileList = new List<Models.File>();
        var uri  = new Uri(url);

        string shareToken = await GetOcsShareToken(projectId);
        
        var propfindParameters = new PropfindParameters{ ApplyTo = ApplyTo.Propfind.ResourceAndAncestors };
        var result = await this.webDav.Propfind(url, propfindParameters);

        if (result.IsSuccessful)
        {
            foreach (var res in result.Resources)
            {
                if(res.IsCollection){
                    this.logger.LogDebug("📁 " + res.Uri);
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
                    Url: new Uri($"{this.baseUrl}/s/{shareToken}/download?path=%2F{dirPath}&files={fileName}")
                ));
            }
        }else{
            this.logger.LogError($"GetFiles ERROR listing files from WebDAV {projectId}");
            this.logger.LogError(result.ToString());
        }

        return fileList;
    }

    public async Task SetupProject(string projectId)
    {
        bool projectExist = await this.ProjectExist(projectId);
        if(projectExist){
            return;
        }

        await this.webDav.Mkcol($"{this.webDavBaseUrl}/public-datasets/{projectId}");
        await this.webDav.Mkcol($"{this.webDavBaseUrl}/public-datasets/{projectId}/data");
    }

    public async Task<string> GetOcsShareToken(string projectId){
		OcsShare? share = await this.GetOcsShare(projectId);
        
        if(share is null){
            share = await this.CreateOcsShare(projectId);
        }
        
        if(share?.token is not null){
            return share.token;
        }

        this.logger.LogError($"GetOcsShareToken ERROR for {projectId}");
        return "NO-TOKEN";
    }

    private async Task<OcsShare?> GetOcsShare(string projectId){
        //TODO: public/private handling
        Uri shareApiUri = new Uri($"{this.baseUrl}/ocs/v2.php/apps/files_sharing/api/v1/shares?path=public-datasets/{projectId}");

		using (var request = new HttpRequestMessage(HttpMethod.Get, shareApiUri))
		{
			request.Headers.Add("OCS-APIRequest", "true");
			request.Headers.Add("Accept", "application/json");
            var response = await this.httpClient.SendAsync(request);

			response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync();
            this.logger.LogDebug("🌐 GetOcsShare response: " + responseString);
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
        Uri shareApiUri = new Uri($"{this.baseUrl}/ocs/v2.php/apps/files_sharing/api/v1/shares");

        var values = new Dictionary<string, string>
        {
            { "path", $"public-datasets/{projectId}" }, //TODO: public/private handling
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
            var response = await this.httpClient.SendAsync(request);

			response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync();
            this.logger.LogDebug("🌐 CreateOcsShare response: " + responseString);
            OcsPostResponse ocsResponse = JsonSerializer.Deserialize<OcsPostResponse>(responseString)!;
            
            if(ocsResponse?.ocs?.data is not null){
                return ocsResponse?.ocs?.data;
            }
        }
        return null;
    }

}
