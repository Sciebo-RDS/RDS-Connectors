namespace DorisScieboRdsConnector.Services.Storage;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DorisScieboRdsConnector.RoCrate;

public interface IStorageService
{
    Task SetupProject(string projectId);

    Task<bool> ProjectExists(string projectId);

    Task<string?> GetProjectName(string projectId);

    Task AddFile(string projectId, string fileName, string contentType, Stream stream);

    Task<IEnumerable<RoFile>> GetFiles(string projectId);
}
