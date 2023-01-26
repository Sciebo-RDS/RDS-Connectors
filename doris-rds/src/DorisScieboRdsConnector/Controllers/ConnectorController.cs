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
        // Do we need tagging or something similar when creating the bucket?


        logger.LogInformation($"CreateProject (POST /metadata/project), userId: {request.UserId}, metadata: {request.Metadata}");

        return Ok(new
        {
            ProjectId = Guid.NewGuid().ToString(), // Is UUID the right choice here? Or something more human readable?
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
        // Generate RO-Crate manifest with file metadata from storage and possibly project label from Describo manifest
        // Post manifest to index server at SND

        logger.LogInformation($"PublishProject (PUT /metadata/project/{projectId}), userId: {request.UserId}");

        return NoContent();
    }
}