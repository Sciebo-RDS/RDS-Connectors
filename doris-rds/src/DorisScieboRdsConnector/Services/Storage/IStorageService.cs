namespace DorisScieboRdsConnector.Services.Storage;

using DorisScieboRdsConnector.RoCrate;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public interface IStorageService
{
    Task SetupProject(string projectId);

    Task<bool> ProjectExists(string projectId);

    Task<string?> GetDataReviewLink(string projectId);

    Task StoreRoCrateMetadata(string projectId, string metadata);

    Task<string?> GetRoCrateMetadata(string projectId);

    Task AddFile(string projectId, string fileName, Stream stream);

    Task<IEnumerable<RoFile>> GetFiles(string projectId);
}
