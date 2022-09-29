using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DorisScieboRdsConnector.Services.Storage;

public interface IStorageService
{
    Task SetupProject(string projectId);

    Task AddFile(string projectId, string fileName, Stream filedata);

    IAsyncEnumerable<Models.File> GetFiles(string projectId);
}
