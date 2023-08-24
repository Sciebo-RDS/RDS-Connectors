using DorisScieboRdsConnector.Models;
using DorisScieboRdsConnector.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using System;
using System.Text.Json.Nodes;

namespace DorisScieboRdsConnector.Controllers;

[ApiController]
[Route("/")]
public class ConnectorController : ControllerBase
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;
    private IStorageService storageService;

    public ConnectorController(ILogger<ConnectorController> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
        MinioClient minio = new MinioClient()
                            .WithEndpoint(configuration["S3:Url"])
                            .WithCredentials(configuration["S3:AccessKey"], configuration["S3:SecretKey"])
                            .WithSSL(false)
                            .Build();
        this.storageService = new S3StorageService(minio, this.logger);
    }

    [HttpPost("metadata/project")]
    public IActionResult CreateProject(PortUserNameWithMetadata request)
    {
        // Generate project identifier
        // Create storage space/path/bucket based on project identifier
        // Do we need tagging or something similar when creating the bucket?
        logger.LogInformation($"CreateProject (POST /metadata/project), userId: {request.UserId}, metadata: {request.Metadata}");
 
        var projectId = Guid.NewGuid().ToString();
        
        logger.LogInformation($"🪣 call SetupProject for projectId {projectId}");
        this.storageService.SetupProject(projectId);

        return Ok(new
        {
            ProjectId = projectId, // Is UUID the right choice here? Or something more human readable?
            Metadata = new JsonObject() // How is this used?
        });
    }
    
    [HttpPatch("metadata/project/{projectId}")]
    public IActionResult UpdateMetadata(string projectId, PortUserNameWithMetadata request)
    {
        // Can we create a profile with only a project label? We can use the label in Doris when selecting manifest.
        // Store the metadata somewhere, so that we can access project label later when generating the Doris
        // RO-Crate manifest? Or can we fetch it from Sciebo RDS somehow?

        logger.LogInformation($"UpdateMetadata (PATCH /metadata/project/{projectId}), userId: {request.UserId}, metadata: {request.Metadata}");

        return Ok(new 
        { 
            Metadata = new JsonObject() 
        });
    }

    [HttpPut("metadata/project/{projectId}")]
    public NoContentResult PublishProject(string projectId, PortUserName request)
    {
        // Check that project has been created in storage
        // Generate RO-Crate manifest with file metadata from storage and possibly project label from Describo manifest
        // Post manifest to index server at SND

        logger.LogInformation($"PublishProject (PUT /metadata/project/{projectId}), userId: {request.UserId}");

        return NoContent();
    }

    [HttpPost("metadata/project/{projectId}/files")]
    public IActionResult AddFile([FromRoute]string projectId, [FromForm]IFormFile files, [FromForm]string fileName, [FromForm]string folder, [FromForm]string userId)
    {
        //TODO: Check that project has been created in storage
        
        logger.LogInformation($"AddFile (POST /metadata/project/{projectId}), file: {fileName}, folder: {folder}");
        
        // Upload file to s3 storage
        this.storageService.AddFile(projectId, fileName, files.ContentType, files.OpenReadStream());

        return Ok(new
        {
            Success = true
        });
    }

    [HttpGet("metadata/project/{projectId}/files")]
    public IActionResult GetFiles([FromRoute]string projectId)
    {
        //TODO: Check that project has been created in storage
        
        logger.LogInformation($"GetFiles (GET /metadata/project/{projectId}), projectId: {projectId}");
        
        // Get file to s3 storage
        var files = this.storageService.GetFiles(projectId);

        return Ok(new
        {
            Files = files
        });
    }
}