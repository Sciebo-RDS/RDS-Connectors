namespace DorisScieboRdsConnector.Controllers;

using DorisScieboRdsConnector.Models;
using DorisScieboRdsConnector.Services.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

[ApiController]
[Route("/")]
public class ConnectorController : ControllerBase
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;
    private readonly IStorageService storageService;

    public ConnectorController(ILogger<ConnectorController> logger, IConfiguration configuration, IStorageService storageService)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.storageService = storageService;
    }

    [HttpPost("metadata/project")]
    public async Task<IActionResult> CreateProject(PortUserNameWithMetadata request)
    {
        // Generate project identifier
        // Create storage space/path/bucket based on project identifier
        // Do we need tagging or something similar when creating the bucket?
        logger.LogInformation("CreateProject (POST /metadata/project), userId: {userId}, metadata: {metadata}", request.UserId, request.Metadata);

        static string GenerateProjectId()
        {
            var bytes = new byte[4];
            new Random().NextBytes(bytes);
            return $"{DateTime.UtcNow.Year}-{Convert.ToHexString(bytes).ToLower()}";
        }

        string projectId = GenerateProjectId();

        while (await storageService.ProjectExists(projectId))
        {
            projectId = GenerateProjectId();
        }

        logger.LogInformation("CreateProject call storageService.SetupProject for {projectId} NextCloud:User: {nextCloudUser}",
            projectId, configuration.GetValue<string>("NextCloud:User"));
        await storageService.SetupProject(projectId);

        return Ok(new
        {
            ProjectId = projectId,
            Metadata = new JsonObject() // TODO How is this used by sciebo-rds?
        });
    }

    [HttpPatch("metadata/project/{projectId}")]
    public async Task<IActionResult> UpdateMetadata(string projectId, PortUserNameWithMetadata request)
    {
        // Can we create a profile with only a project label? We can use the label in Doris when selecting manifest.
        // Store the metadata somewhere, so that we can access project label later when generating the Doris
        // RO-Crate manifest? Or can we fetch it from Sciebo RDS somehow?

        logger.LogInformation("UpdateMetadata (PATCH /metadata/project/{projectId}), userId: {userId}, metadata: {metadata}",
            projectId, request.UserId, request.Metadata);

        var files = await storageService.GetFiles(projectId);
        //var manifest = RoCrateHelper.GenerateRoCrateManifest(projectId, this.configuration["Domain"], "usertmp", files);

        return Ok(new
        {
            Metadata = new JsonObject() // TODO What should we return here? How is this used by sciebo-rds?
        });
    }

    [HttpPut("metadata/project/{projectId}")]
    public async Task<NoContentResult> PublishProject(string projectId, PortUserName request)
    {
        // Check that project has been created in storage
        // Generate RO-Crate manifest with file metadata from storage and possibly project label from Describo manifest
        // Post manifest to index server at SND

        var files = await storageService.GetFiles(projectId);

        logger.LogInformation("PublishProject (PUT /metadata/project/{projectId}), userId: {userId}", projectId, request.UserId);

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
    public async Task<IActionResult> AddFile([FromRoute] string projectId)
    {
        if (Request.Form.Files.Count == 0)
        {
            return NotFound(new
            {
                Success = false,
                Message = "Missing file in POST"
            });
        }

        if (!await storageService.ProjectExists(projectId))
        {
            return NotFound(new
            {
                Success = false,
                Message = $"Project {projectId} does not exist in storage"
            });
        }

        foreach (var file in Request.Form.Files)
        {
            logger.LogInformation("📄AddFile IFormFile file: {fileName}", file.FileName);

            await storageService.AddFile(projectId, file.FileName, RoFileType.data, file.ContentType, file.OpenReadStream());
        }

        return Ok(new
        {
            Success = true
        });
    }

    [HttpGet("metadata/project/{projectId}/files")]
    public async Task<IActionResult> GetFiles([FromRoute] string projectId)
    {
        logger.LogInformation("GetFiles (GET /metadata/project/{projectId}), projectId: {projectId}", projectId, projectId);

        var files = await storageService.GetFiles(projectId);

        return Ok(new
        {
            Files = files
        });
    }

    [HttpGet("debug/configuration")]
    public JsonResult DebugGetConfiguration()
    {
        return new JsonResult(configuration.AsEnumerable());
    }
}