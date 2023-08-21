using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DorisScieboRdsConnector.Services.Storage;

using Minio;
using Minio.DataModel;
using Minio.DataModel.ILM;
using Minio.Exceptions;
using System;
using System.Collections;
using Microsoft.Extensions.Logging;

public class S3StorageService : IStorageService
{
    private readonly ILogger logger;
    public MinioClient minio;
    public S3StorageService(string? endpoint, string? accessKey, string? secretKey, bool secure, ILogger logger)
    {
        this.logger = logger;
        this.minio = new MinioClient()
                            .WithEndpoint(endpoint)
                            .WithCredentials(accessKey, secretKey)
                            .WithSSL(secure)
                            .Build();
    }
    public async Task AddFile(string projectId, string fileName, Stream filedata)
    {
        var args = new PutObjectArgs()
            .WithBucket(projectId)
            .WithObject(fileName)
            .WithStreamData(filedata)
            .WithObjectSize(filedata.Length)
            .WithContentType("application/octet-stream");
        await this.minio.PutObjectAsync(args).ConfigureAwait(false);

        //throw new System.NotImplementedException();
    }

    public IAsyncEnumerable<Models.File> GetFiles(string projectId)
    {
        throw new System.NotImplementedException();
    }

    public async Task SetupProject(string projectId)
    {
        logger.LogInformation($"🪣 START SetupProject with {projectId}");
        try
        {
            await this.minio.MakeBucketAsync(
                    new MakeBucketArgs()
                        .WithBucket(projectId)
                ).ConfigureAwait(false);

            logger.LogInformation($"🪣 BUCKET {projectId} was created successfully");
        }catch(MinioException e){
            logger.LogInformation($"🪣🪣🪣🪣 FAIL BUCET  {e.Message}");
            throw e;
        }
    }
}
