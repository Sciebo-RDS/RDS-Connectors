namespace DorisScieboRdsConnector.Services.Storage;

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DorisScieboRdsConnector.RoCrate;

public interface IStorageService
{
    Task SetupProject(string projectId);

    Task<bool> ProjectExists(string projectId);

    Task<string?> GetDataReviewLink(string projectId);

    Task StoreRoCrateMetadata(string projectId, string metadata);

    Task<string?> GetRoCrateMetadata(string projectId);

    Task AddFile(string projectId, string fileName, string contentType, Stream stream);

    Task<IEnumerable<RoFile>> GetFiles(string projectId);
}
