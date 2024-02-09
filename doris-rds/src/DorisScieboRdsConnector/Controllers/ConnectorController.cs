namespace DorisScieboRdsConnector.Controllers;

using DorisScieboRdsConnector.Configuration;
using DorisScieboRdsConnector.Controllers.Filters;
using DorisScieboRdsConnector.Controllers.Models;
using DorisScieboRdsConnector.RoCrate;
using DorisScieboRdsConnector.Services.Doris;
using DorisScieboRdsConnector.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

[ApiController]
[Route("/")]
public class ConnectorController(
    ILogger<ConnectorController> logger,
    IConfiguration configuration,
    IOptions<DorisConfiguration> dorisConfiguration,
    IOptions<NextCloudConfiguration> nextCloudConfiguration,
    IStorageService storageService,
    IDorisService dorisService) : ControllerBase
{
    private readonly ILogger logger = logger;
    private readonly IConfiguration configuration = configuration;
    private readonly DorisConfiguration dorisConfiguration = dorisConfiguration.Value;
    private readonly NextCloudConfiguration nextCloudConfiguration = nextCloudConfiguration.Value;
    private readonly IStorageService storageService = storageService;
    private readonly IDorisService dorisService = dorisService;

    private const string roCrateFileName = "ro-crate-metadata.json";

    [HttpPost("metadata/project")]
    public async Task<IActionResult> CreateProject(PortUserName request)
    {
        logger.LogInformation("Entering CreateProject (POST /metadata/project), userId: {userId}", request.UserId);

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
        logger.LogInformation("Entering UpdateMetadata (PATCH /metadata/project/{projectId}), userId: {userId}, metadata: {metadata}",
            projectId, request.UserId, request.Metadata);

        await storageService.StoreRoCrateMetadata(projectId, request.Metadata.RootElement.GetRawText());

        return Ok(request.Metadata);
    }

    [HttpPost("metadata/project/{projectId}/files")]
    // Disable form value model binding to ensure that files are not buffered
    [DisableFormValueModelBinding]
    // Disable request size limit to allow streaming large files
    [DisableRequestSizeLimit]
    public async Task<IActionResult> AddFile([FromRoute] string projectId)
    {
        logger.LogInformation("Entering 📄AddFile (PUT metadata/project/{projectId})", projectId);

        if (!await storageService.ProjectExists(projectId))
        {
            logger.LogInformation("📄AddFile: project {projectId} not found in storage, aborting", projectId);

            return NotFound(new
            {
                Success = false,
                Message = $"Project {projectId} does not exist in storage."
            });
        }

        var request = HttpContext.Request;

        // Validation of Content-Type:
        // 1. It must be a form-data request
        // 2. A boundary should be found in the Content-Type
        if (!request.HasFormContentType ||
            !MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaTypeHeader) ||
            string.IsNullOrEmpty(mediaTypeHeader.Boundary.Value))
        {
            logger.LogInformation("📄AddFile: could not parse as multipart request, aborting.");
            return new UnsupportedMediaTypeResult();
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaTypeHeader.Boundary.Value).Value!;
        var reader = new MultipartReader(boundary, request.Body);
        var section = await reader.ReadNextSectionAsync();

        while (section != null)
        {
            if (ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition) &&
                contentDisposition.DispositionType.Equals("form-data") &&
                !string.IsNullOrEmpty(contentDisposition.FileName.Value))
            {
                string fileName = contentDisposition.FileName.Value;

                if (fileName.EndsWith('/'))
                {
                    // RDS sometimes sends directories, ignore
                    logger.LogDebug("📄AddFile: Received directory {fileName}, skipping", fileName);
                }
                else if (fileName == roCrateFileName)
                {
                    // We ignore ro-crate-metadata.json here, since we already stored it in UpdateMetadata
                    logger.LogDebug("📄AddFile: Received ro-crate-metadata.json, skipping");
                }
                else
                {
                    logger.LogInformation("📄AddFile: Store {fileName}...", fileName);

                    await storageService.AddFile(projectId, fileName, section.Body);

                    logger.LogInformation("📄AddFile: {fileName} stored.", fileName);
                }
            }
            else
            {
                logger.LogDebug("📄AddFile: Non filename section found, header: {header}, content: {content}",
                    section.ContentDisposition,
                    await section.ReadAsStringAsync());
            }

            section = await reader.ReadNextSectionAsync();
        }

        logger.LogInformation("📄AddFile: Done, returning success");

        return Ok(new
        {
            Success = true
        });
    }

    [HttpPut("metadata/project/{projectId}")]
    public async Task<IActionResult> PublishProject(string projectId, PortUserName request)
    {
        static string? GetProjectName(string roCrateMetadata)
        {
            var roCrate = JsonDocument.Parse(roCrateMetadata);
            if (roCrate.RootElement.TryGetProperty("@graph", out var graph) &&
                graph.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement element in graph.EnumerateArray())
                {
                    if (element.TryGetProperty("@id", out var id) &&
                        element.TryGetProperty("@type", out var type) &&
                        element.TryGetProperty("name", out var name))
                    {
                        if (id.ToString() == "./" && type.ToString() == "Dataset")
                        {
                            return name.ToString();
                        }
                    }
                }
            }

            return null;
        }

        logger.LogInformation("Entering PublishProject (PUT metadata/project/{projectId}), userId: {userId}", projectId, request.UserId);

        var files = await storageService.GetFiles(projectId);
        string? dataReviewLink = await storageService.GetDataReviewLink(projectId);
        string? roCrateMetadata = await storageService.GetRoCrateMetadata(projectId);
        string? projectName = null;

        if (roCrateMetadata != null)
        {
            projectName = GetProjectName(roCrateMetadata);
        }

        var roCrate = new RoCrate(
            projectId: projectId,
            eduPersonPrincipalName: request.GetUserName(),
            principalDomain: dorisConfiguration.PrincipalDomain,
            name: projectName,
            dataReviewLink: dataReviewLink,
            files: files);

        var json = roCrate.ToGraph();
        logger.LogDebug("RO-Crate payload: {payload}", json);

        if (dorisConfiguration.DorisApiEnabled)
        {
            await dorisService.PostRoCrate(json);
        }
        else
        {
            logger.LogInformation("Doris API disabled, not posting RO-Crate payload.");
        }

        await storageService.StoreRoCrateMetadata(projectId, json.ToJsonString());

        return Content("{\"DOI\": \"Provided later via doris.snd.se\"}", "application/json");

        /*return Ok(new
        {
            DOI = "Provided later via doris.snd.se"
        });*/
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