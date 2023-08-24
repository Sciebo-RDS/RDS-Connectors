using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DorisScieboRdsConnector.Services.Storage;


public interface IStorageService
{
    Task SetupProject(string projectId);

    Task<bool> ProjectExist(string projectId);

    Task AddFile(string projectId, string fileName, string contentType, Stream filedata);

    Task<IEnumerable<Models.File>> GetFiles(string projectId);
}
