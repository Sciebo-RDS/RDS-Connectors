namespace DorisScieboRdsConnector.Services.Storage;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WebDav;
using System.Reactive.Linq;

using System;
using Microsoft.Extensions.Logging;

public class WebDavStorageService : IStorageService
{
    private readonly ILogger logger;
    private IWebDavClient webDav;

    private string baseUrl;

    public WebDavStorageService(IWebDavClient webDav, ILogger logger, string baseUrl)
    {
        this.logger = logger;
        this.webDav = webDav;
        this.baseUrl = baseUrl;
    }
    public async Task AddFile(string projectId, string fileName, string contentType, Stream filedata)
    {
        string fileUrl = this.baseUrl + projectId + "/" + fileName;
        this.logger.LogDebug("AddFile fileUrl 🐛 " + fileUrl);
        this.logger.LogDebug("AddFile contentType 🐛 " + contentType);
        var result = await this.webDav.PutFile(fileUrl, filedata, contentType);
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
                
                fileList.Add(new(
                    Id : filePath,
                    ContentSize : res.ContentLength.ToString(),
                    EncodingFormat : res.ContentType,
                    DateModified : res.LastModifiedDate,
                    Md5 : res.ETag.Trim('"')
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

        await this.webDav.Mkcol(this.baseUrl + projectId);
        await this.webDav.Mkcol(this.baseUrl + projectId + "/data");
    }
}
