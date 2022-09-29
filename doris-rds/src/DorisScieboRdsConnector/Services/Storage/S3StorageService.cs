using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DorisScieboRdsConnector.Services.Storage;

public class S3StorageService : IStorageService
{
    public Task AddFile(string projectId, string fileName, Stream filedata)
    {
        throw new System.NotImplementedException();
    }

    public IAsyncEnumerable<Models.File> GetFiles(string projectId)
    {
        throw new System.NotImplementedException();
    }

    public Task SetupProject(string projectId)
    {
        throw new System.NotImplementedException();
    }
}
