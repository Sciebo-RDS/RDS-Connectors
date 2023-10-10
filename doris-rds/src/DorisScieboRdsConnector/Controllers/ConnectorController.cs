using DorisScieboRdsConnector.Models;
using DorisScieboRdsConnector.Services.Storage;
using DorisScieboRdsConnector.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebDav;
using System.Net.Http;
using System.Net.Http.Headers;
using System;
using System.Text.Json.Nodes;

namespace DorisScieboRdsConnector.Controllers;

[ApiController]
[Route("/")]
public class ConnectorController : ControllerBase
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;
    private readonly IStorageService storageService;

    public ConnectorController(ILogger<ConnectorController> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
        
        string authString = configuration.GetValue<string>("NextCloud:User") + ":" + configuration.GetValue<string>("NextCloud:Password");
        string basicAuth = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authString));

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        httpClient.DefaultRequestHeaders.Add("Host", "localhost");

        var webDav = new WebDavClient(httpClient);
        this.storageService = new NextCloudStorageService(webDav, httpClient, logger, configuration);
    }

    [HttpPost("metadata/project")]
    public IActionResult CreateProject(PortUserNameWithMetadata request)
    {
        // Generate project identifier
        // Create storage space/path/bucket based on project identifier
        // Do we need tagging or something similar when creating the bucket?
        logger.LogInformation($"CreateProject (POST /metadata/project), userId: {request.UserId}, metadata: {request.Metadata}");
 
        string hashCode = String.Format("{0:X}", Guid.NewGuid().ToString().GetHashCode()).ToLower();
        string projectId =  $"{DateTime.Now.Year}-{hashCode}";
        
        /*
        while(await this.storageService.ProjectExist(projectId)){
            hashCode = String.Format("{0:X}", Guid.NewGuid().ToString().GetHashCode()).ToLower();
            projectId =  $"{DateTime.Now.Year}-{hashCode}";
        }*/
        
        logger.LogInformation($"🪣CreateProject call storageService.SetupProject for {projectId} NextCloud:User: {configuration.GetValue<string>("NextCloud:User")}");
        storageService.SetupProject(projectId);

        return Ok(new
        {
            ProjectId = projectId,
            Metadata = new JsonObject() // How is this used by sciebo-rds?
        });
    }
    
    [HttpPatch("metadata/project/{projectId}")]
    public IActionResult UpdateMetadata(string projectId, PortUserNameWithMetadata request)
    {
        // Can we create a profile with only a project label? We can use the label in Doris when selecting manifest.
        // Store the metadata somewhere, so that we can access project label later when generating the Doris
        // RO-Crate manifest? Or can we fetch it from Sciebo RDS somehow?

        logger.LogInformation($"UpdateMetadata (PATCH /metadata/project/{projectId}), userId: {request.UserId}, metadata: {request.Metadata}");

        var files = storageService.GetFiles(projectId);
        //var manifest = RoCrateHelper.GenerateRoCrateManifest(projectId, this.configuration["Domain"], "usertmp", files);

        return Ok(new 
        { 
            Metadata = new JsonObject(),
            User = request.GetUserName() 
        });
    }

    [HttpPut("metadata/project/{projectId}")]
    public NoContentResult PublishProject(string projectId, PortUserName request)
    {
        // Check that project has been created in storage
        // Generate RO-Crate manifest with file metadata from storage and possibly project label from Describo manifest
        // Post manifest to index server at SND

        var files = storageService.GetFiles(projectId);

        logger.LogInformation($"PublishProject (PUT /metadata/project/{projectId}), userId: {request.UserId}");

        return NoContent();
    }

    /// <summary>
    /// Handle upload of each file via the connector
    /// for the DORIS connector each file will be stored in a S3-bucket.
    /// </summary>
    /// <param name="projectId">The id for the project (matches the S3 bucket name)</param>
    /// <param name="files">A file</param>
    /// <param name="fileName">Not used but suplied by the sender</param>
    /// <param name="folder">Not used but suplied by the sender</param>
    /// <param name="userId">The user identifier</param>
    /// <returns></returns>
    [HttpPost("metadata/project/{projectId}/files")]
    [Consumes("multipart/form-data")]
    //public IActionResult AddFile([FromRoute]string projectId, [FromForm]IFormFile files, [FromForm]string fileName, [FromForm]string folder, [FromForm]string userId)
    public IActionResult AddFile([FromRoute]string projectId)
    {
        if(Request.Form.Files.Count == 0){
            return NotFound(new {
                Success = false,
                Message = "Missing file in POST"
            });
        }

        if(storageService.ProjectExist(projectId).Result == false){
            return NotFound(new {
                Success = false,
                Message = $"Project {projectId} does not have a storage bucket"
            });
        }
        
        foreach(IFormFile file in Request.Form.Files){
            logger.LogInformation($"📄AddFile IFormFile file: {file.FileName}");
            storageService.AddFile(projectId, file.FileName, file.ContentType, file.OpenReadStream());
        }
    
        return Ok(new
        {
            Success = true
        });
    }

    [HttpGet("metadata/project/{projectId}/files")]
    public IActionResult GetFiles([FromRoute]string projectId)
    {
        logger.LogInformation($"GetFiles (GET /metadata/project/{projectId}), projectId: {projectId}");
        
        var files = storageService.GetFiles(projectId);
        return Ok(new
        {
            Files = files.Result
        });
    }
}