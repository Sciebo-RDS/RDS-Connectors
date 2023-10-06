namespace DorisScieboRdsConnector.Services.Storage;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WebDav;
using Microsoft.Extensions.Configuration;

using System;
using Microsoft.Extensions.Logging;
using System.Linq;

public class WebDavStorageService : IStorageService
{
    private readonly ILogger logger;
    private IWebDavClient webDav;
    private IConfiguration configuration;

    private string? baseUrl;

    public WebDavStorageService(IWebDavClient webDav, ILogger logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.webDav = webDav;
        this.configuration = configuration;
        this.baseUrl = configuration.GetValue<string>("NextCloud:WebDavBaseUrl");
    }
    public async Task AddFile(string projectId, string fileName, string contentType, Stream stream)
    {
        string fileUrl = this.baseUrl + projectId + "/" + fileName;
        this.logger.LogDebug("AddFile fileUrl 🐛 " + fileUrl);
        this.logger.LogDebug("AddFile contentType 🐛 " + contentType);
        var result = await this.webDav.PutFile(fileUrl, stream, contentType);
        if(result.IsSuccessful){
            this.logger.LogDebug("AddFile OK 🐛 "+fileUrl);
        }else{
            this.logger.LogError("AddFile UPLOAD FAIL 🐛 "+fileUrl);
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
        var url = this.baseUrl + projectId;
        var fileList = new List<Models.File>();
        var uri  = new Uri(url);
        string nextCludBaseUrl = uri.GetLeftPart(System.UriPartial.Authority);

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
                
                string fileUrlString = $"{nextCludBaseUrl}/s/{shareId}/download?path=%2F{dirPath}&files={fileName}";

                Uri fileUrl = new Uri(fileUrlString);

                fileList.Add(new(
                    Id : filePath,
                    ContentSize : res.ContentLength.ToString(),
                    EncodingFormat : res.ContentType,
                    DateModified : res.LastModifiedDate,
                    Md5 : res.ETag.Trim('"'),
                    Url: fileUrl
                ));
            }
        }else{
            this.logger.LogError("GetFiles ERROR listing files from WebDAV "+projectId);
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
        return "s94BD69BqGKPW35";
    }
}
