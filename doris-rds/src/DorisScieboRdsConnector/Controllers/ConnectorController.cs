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

    [HttpPost("metadata/project")]
    public IActionResult CreateProject(PortUserNameWithMetadata request)
    {
        // Generate project identifier
        // Create storage space/path/bucket based on project identifier

        logger.LogInformation($"CreateProject (POST /metadata/project), userId: {request.UserId}, metadata: {request.Metadata}");

        return Ok(new
        {
            ProjectId = Guid.NewGuid().ToString(),
            Metadata = new JsonObject()
        });
    }

    [HttpPatch("metadata/project/{projectId}")]
    public IActionResult UpdateMetadata(string projectId, PortUserNameWithMetadata request)
    {
        // Do nothing? Can we remove this endpoint?

        logger.LogInformation($"UpdateMetadata (PATCH /metadata/project/{projectId}), userId: {request.UserId}, metadata: {request.Metadata}");

        return Ok(new 
        { 
            Metadata = new JsonObject() 
        });
    }

    [HttpPost("metadata/project/{projectId}/files")]
    public IActionResult AddFile(string projectId, AddFileRequest request)
    {
        // Check that project has been created in storage
        // Upload file to storage

        logger.LogInformation($"AddFile (POST /metadata/project/{projectId}), file: {request.FileName}");

        return Ok(new
        {
            Success = true
        });
    }

    [HttpPut("metadata/project/{projectId}")]
    public NoContentResult PublishProject(string projectId, PortUserName request)
    {
        // Check that project has been created in storage
        // Generate RO-Crate manifest with file metadata from storage
        // Post manifest to index server at SND

        logger.LogInformation($"PublishProject (PUT /metadata/project/{projectId}), userId: {request.UserId}");

        return NoContent();
    }
}