namespace DorisScieboRdsConnector.Services.Storage;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DorisScieboRdsConnector.Models;

public interface IStorageService
{
    Task SetupProject(string projectId);

    Task<bool> ProjectExists(string projectId);

    Task AddFile(string projectId, string fileName, RoFileType type, string contentType, Stream stream);

    Task<IEnumerable<RoFile>> GetFiles(string projectId);
}
