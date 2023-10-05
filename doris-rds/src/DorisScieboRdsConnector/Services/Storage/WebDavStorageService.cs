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
    private PropfindParameters propfindParameters;

    public WebDavStorageService(IWebDavClient webDav, ILogger logger)
    {
        this.logger = logger;
        this.webDav = webDav;

        this.propfindParameters = new PropfindParameters
        {
            Headers = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Depth", "infinity")
            }
        };
    }
    public async Task AddFile(string projectId, string fileName, string contentType, Stream filedata)
    {
        throw new System.NotImplementedException();
    }

    public Task<bool> ProjectExist(string projectId){
        
        throw new System.NotImplementedException();
    }

    public async Task<IEnumerable<Models.File>> GetFiles(string projectId)
    {   
        var url = "http://nextcloud/remote.php/dav/files/datasets/" + projectId;
        var fileList = new List<Models.File>();

        

        var result = await this.webDav.Propfind(url, this.propfindParameters);
        
        if (result.IsSuccessful)
        {
            foreach (var res in result.Resources)
            {
                if(res.IsCollection){
                    this.logger.LogInformation("📁 " + res.Uri);
                }else{
                    
                    this.logger.LogInformation("📄 " + res.Uri);
                    this.logger.LogInformation("📄----- ContentType: " + res.ContentType);
                    this.logger.LogInformation("📄----- DisplayName: " + res.DisplayName);
                    this.logger.LogInformation("📄----- CreationDate: " + res.CreationDate);
                    this.logger.LogInformation("📄----- LastModifiedDate: " + res.LastModifiedDate);
                    this.logger.LogInformation("📄----- ETag: " + res.ETag);
                    this.logger.LogInformation("📄----- ContentLength: " + res.ContentLength);
                    
                    fileList.Add(new(
                        Id : res.Uri,
                        ContentSize : res.ContentLength,
                        EncodingFormat: res.ContentType
                    ));
                }
            }
        }else{
            this.logger.LogError("error listing files from WebDAV");
            this.logger.LogError(result.ToString());
        }

        return fileList;
    }

    public async Task SetupProject(string projectId)
    {
        logger.LogInformation($"🪣 START SetupProject with {projectId}");

    }
}
