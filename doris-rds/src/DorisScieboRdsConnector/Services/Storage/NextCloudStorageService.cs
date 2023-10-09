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

public class NextCloudStorageService : IStorageService
{
    private readonly ILogger logger;
    private IWebDavClient webDav;
    private HttpClient httpClient;
    private IConfiguration configuration;

    private string? baseUrl;
    private string? nextCloudUser;

    public NextCloudStorageService(IWebDavClient webDav, HttpClient httpClient, ILogger logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.webDav = webDav;
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.baseUrl = configuration.GetValue<string>("NextCloud:BaseUrl");
        this.nextCloudUser = configuration.GetValue<string>("NextCloud:User");
    }
    public async Task AddFile(string projectId, string fileName, string contentType, Stream stream)
    {
        string fileUploadUrl = $"{this.baseUrl}/remote.php/dav/files/{this.nextCloudUser}/{projectId}/{fileName}";
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
        var result = await this.webDav.Propfind(this.baseUrl + projectId);
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
        var url = $"{this.baseUrl}/remote.php/dav/files/{this.nextCloudUser}/{projectId}";
        var fileList = new List<Models.File>();
        var uri  = new Uri(url);

        //TODO: get share id from NextCloud
        string shareId = await GetShareId(projectId);
        
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
                    Url: new Uri($"{this.baseUrl}/s/{shareId}/download?path=%2F{dirPath}&files={fileName}")
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

        await this.webDav.Mkcol(this.configuration["NextCloud."]+ projectId);
        await this.webDav.Mkcol(this.baseUrl + projectId + "/data");
    }

    private async Task<string> GetShareId(string projectId){
        Uri shareApiUri = new Uri($"{this.baseUrl}/ocs/v2.php/apps/files_sharing/api/v1/shares?path={projectId}");

		using (var request = new HttpRequestMessage(HttpMethod.Get, shareApiUri))
		{
			request.Headers.Add("OCS-APIRequest", "true");// = new AuthenticationHeaderValue("Bearer", Token);
			request.Headers.Add("Accept", "application/json");
            var response = await this.httpClient.SendAsync(request);

			response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync();
            this.logger.LogDebug("🌐 SHARE response: " + responseString);
            OcsResponse share = JsonSerializer.Deserialize<OcsResponse>(responseString)!;
            string? token = share?.ocs?.data?[0]?.token;
            if(token != null){
                this.logger.LogDebug("🌐 TOKEN: " + token);
                return token;
            }
		}

        //TODO: check if share exist, use this share
        
        //TODO: create share
        /*
        Header:
            Accept application/json
            OCS-APIRequest true
        POST body:
            path projectId
            permissions 1
            shareType 3
            publicUpload false
        */
        return "";
    }
}
