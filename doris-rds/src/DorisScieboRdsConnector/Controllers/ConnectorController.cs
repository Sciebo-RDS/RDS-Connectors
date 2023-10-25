namespace DorisScieboRdsConnector.Controllers;

using DorisScieboRdsConnector.Configuration;
using DorisScieboRdsConnector.Controllers.Models;
using DorisScieboRdsConnector.RoCrate;
using DorisScieboRdsConnector.Services.Doris;
using DorisScieboRdsConnector.Services.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

[ApiController]
[Route("/")]
public class ConnectorController : ControllerBase
{
    private readonly ILogger logger;
    private readonly IConfiguration configuration;
    private readonly DorisConfiguration dorisConfiguration;
    private readonly NextCloudConfiguration nextCloudConfiguration;
    private readonly IStorageService storageService;
    private readonly IDorisService dorisService;

    public ConnectorController(
        ILogger<ConnectorController> logger, 
        IConfiguration configuration,
        IOptions<DorisConfiguration> dorisConfiguration,
        IOptions<NextCloudConfiguration> nextCloudConfiguration,
        IStorageService storageService,
        IDorisService dorisService)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.dorisConfiguration = dorisConfiguration.Value;
        this.nextCloudConfiguration = nextCloudConfiguration.Value;
        this.storageService = storageService;
        this.dorisService = dorisService;
    }

    [HttpPost("metadata/project")]
    public async Task<IActionResult> CreateProject(PortUserNameWithMetadata request)
    {
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
            projectId, nextCloudConfiguration.User);
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
        logger.LogInformation("UpdateMetadata (PATCH /metadata/project/{projectId}), userId: {userId}, metadata: {metadata}",
            projectId, request.UserId, request.Metadata);

        //var files = await storageService.GetFiles(projectId);
        //var manifest = RoCrateHelper.GenerateRoCrateManifest(projectId, this.configuration["Domain"], "usertmp", files);

        return Ok(new
        {
            Metadata = new JsonObject() // TODO What should we return here? How is this used by sciebo-rds?
        });
    }

    [HttpPut("metadata/project/{projectId}")]
    public async Task<NoContentResult> PublishProject(string projectId, PortUserName request)
    {
        logger.LogInformation("PublishProject (PUT metadata/project/{projectId}), userId: {userId}", projectId, request.UserId);

        var files = await storageService.GetFiles(projectId);
        var roCrate = new RoCrate(projectId, request.UserId, dorisConfiguration.PrincipalDomain, files);

        var json = roCrate.ToGraph();
        logger.LogDebug("RO-Crate payload: {payload}", json);

        if (dorisConfiguration.DorisApiEnabled)
        {
            await dorisService.PostRoCrate(roCrate.ToGraph());
        }
        else
        {
            logger.LogInformation("Doris API disabled, not posting RO-Crate payload.");
        }

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

    // Endpoints that are not implemented, are they needed?
    // GET metadata/project/{projectId} - Get all metadata
    // DELETE metadata/project/{projectId} - Remove a project from this service
    // DELETE metadata/project/{projectId}/files - ?
    // GET metadata/project/{projectId}/files/{fileId} - Get specified file
    // PATCH metadata/project/{projectId}/files/{fileId} - ?
    // DELETE metadata/project/{projectId}/files/{fileId} - ?
    // GET metadata/project - Returns all projects available in the service for user
}