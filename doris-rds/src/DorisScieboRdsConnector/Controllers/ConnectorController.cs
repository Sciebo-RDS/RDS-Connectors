using DorisScieboRdsConnector.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json.Nodes;

namespace DorisScieboRdsConnector.Controllers;

[ApiController]
[Route("/")]
public class ConnectorController : ControllerBase
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;

    public ConnectorController(ILogger<ConnectorController> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
    }

    [HttpPost("project")]
    public IActionResult CreateProject(PortUserNameWithMetadata request)
    {
        // Generate project identifier
        // Create storage space/path/bucket based on project identifier

        logger.LogInformation($"CreateProject (POST /project), userId: {request.UserId}, metadata: {request.Metadata}");

        return Ok(new
        {
            ProjectId = Guid.NewGuid().ToString(),
            Metadata = new JsonObject()
        });
    }

    [HttpPatch("project/{projectId}")]
    public IActionResult UpdateMetadata(string projectId, PortUserNameWithMetadata request)
    {
        // Do nothing? Can we remove this endpoint?

        logger.LogInformation($"UpdateMetadata (PATCH project/{projectId}), userId: {request.UserId}, metadata: {request.Metadata}");

        return Ok(new 
        { 
            Metadata = new JsonObject() 
        });
    }

    [HttpPost("project/{projectId}/files")]
    public IActionResult AddFile(string projectId, IFormFile files)
    {
        // Check that project has been created in storage
        // Upload file to storage

        logger.LogInformation($"AddFile (POST project/{projectId}), file: {files.FileName}");

        return Ok(new
        {
            Success = true
        });
    }

    [HttpPut("project/{projectId}")]
    public NoContentResult PublishProject(string projectId, PortUserName request)
    {
        // Check that project has been created in storage
        // Generate RO-Crate manifest with file metadata from storage
        // Post manifest to index server at SND

        logger.LogInformation($"PublishProject (PUT project/{projectId}), userId: {request.UserId}");

        return NoContent();
    }
}