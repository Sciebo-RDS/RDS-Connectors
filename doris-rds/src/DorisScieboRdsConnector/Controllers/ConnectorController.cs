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

    private static readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNamingPolicy = null };
    private const string roCrateFileName = "ro-crate-metadata.json";

    [HttpPost("metadata/project")]
    public async Task<IActionResult> CreateProject(PortUserName request)
    {
        logger.LogDebug("Entering CreateProject (POST /metadata/project), userId: {userId}", request.UserId);

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

        logger.LogDebug("CreateProject call storageService.SetupProject for {projectId} NextCloud:User: {nextCloudUser}",
            projectId, nextCloudConfiguration.User);
        await storageService.SetupProject(projectId);

        logger.LogInformation("Created project {projectId}", projectId);

        return Ok(new
        {
            ProjectId = projectId,
            Metadata = new JsonObject()
        });
    }

    [HttpPatch("metadata/project/{projectId}")]
    public async Task<IActionResult> UpdateMetadata(string projectId, PortUserNameWithMetadata request)
    {
        logger.LogDebug("Entering UpdateMetadata (PATCH /metadata/project/{projectId}), userId: {userId}, metadata: {metadata}",
            projectId, request.UserId, request.Metadata);

        await storageService.StoreRoCrateMetadata(projectId, request.Metadata.RootElement.GetRawText());

        logger.LogInformation("Stored metadata for {projectId}", projectId);

        return Ok(request.Metadata);
    }

    [HttpPost("metadata/project/{projectId}/files")]
    // Disable form value model binding to ensure that files are not buffered
    [DisableFormValueModelBinding]
    // Disable request size limit to allow streaming large files
    [DisableRequestSizeLimit]
    public async Task<IActionResult> AddFile([FromRoute] string projectId)
    {
        logger.LogDebug("Entering 📄AddFile (PUT metadata/project/{projectId})", projectId);

        if (!await storageService.ProjectExists(projectId))
        {
            logger.LogInformation("📄AddFile {projectId}: project not found in storage, aborting", projectId);

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
            logger.LogInformation("📄AddFile {projectId}: could not parse as multipart request, aborting.", projectId);
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
                    logger.LogDebug("📄AddFile {projectId}: Received directory {fileName}, skipping", projectId, fileName);
                }
                else if (fileName == roCrateFileName)
                {
                    // We ignore ro-crate-metadata.json here, since we already stored it in UpdateMetadata
                    logger.LogDebug("📄AddFile {projectId}: Received ro-crate-metadata.json, skipping", projectId);
                }
                else
                {
                    logger.LogDebug("📄AddFile {projectId}: Store {fileName}...", projectId, fileName);

                    await storageService.AddFile(projectId, fileName, section.Body);

                    logger.LogDebug("📄AddFile {projectId}: {fileName} stored.", projectId, fileName);

                    logger.LogInformation("File {fileName} stored for project {projectId}", projectId, fileName); 
                }
            }
            else
            {
                logger.LogDebug("📄AddFile {projectId}: Non filename section found, header: {header}, content: {content}",
                    projectId,
                    section.ContentDisposition,
                    await section.ReadAsStringAsync());
            }

            section = await reader.ReadNextSectionAsync();
        }

        logger.LogDebug("📄AddFile {projectId}: Done, returning success", projectId);

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

        logger.LogDebug("Entering PublishProject (PUT metadata/project/{projectId}), userId: {userId}", projectId, request.UserId);

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
        logger.LogDebug("RO-Crate payload for {projectId}: {payload}", projectId, json);

        if (dorisConfiguration.DorisApiEnabled)
        {
            await dorisService.PostRoCrate(json);
        }
        else
        {
            logger.LogInformation("Doris API disabled, not posting RO-Crate payload for project {projectId}.", projectId);
        }

        await storageService.StoreRoCrateMetadata(projectId, json.ToJsonString());

        logger.LogInformation("Project {projectId} published", projectId);

        // Use JsonResult and custom serializer options to ensure that property names
        // are serialized as is.
        // Sciebo expects "DOI" in upper case, will fail otherwise.
        return new JsonResult(new
        {
            DOI = "Provided later via doris.snd.se"
        },
        jsonSerializerOptions);
    }

    [HttpGet("metadata/project/{projectId}/files")]
    public async Task<IActionResult> GetFiles([FromRoute] string projectId)
    {
        logger.LogDebug("GetFiles (GET /metadata/project/{projectId}), projectId: {projectId}", projectId, projectId);

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