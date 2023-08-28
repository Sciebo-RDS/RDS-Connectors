namespace DorisScieboRdsConnector.Services.Storage;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Tags;
using System.Reactive.Linq;
using Minio.Exceptions;
using System;
using Microsoft.Extensions.Logging;

public class S3StorageService : IStorageService
{
    private readonly ILogger logger;
    public IMinioClient minio;
    public S3StorageService(IMinioClient minio, ILogger logger)
    {
        this.logger = logger;
        this.minio = minio;
    }
    public async Task AddFile(string projectId, string fileName, string contentType, Stream filedata)
    {
        var args = new PutObjectArgs()
            .WithBucket(projectId)
            .WithObject(fileName)
            .WithStreamData(filedata)
            .WithObjectSize(filedata.Length)
            .WithContentType(contentType);
            
        await this.minio.PutObjectAsync(args).ConfigureAwait(false);
    }

    public Task<bool> ProjectExist(string projectId){
        return this.minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(projectId));
        throw new System.NotImplementedException();
    }

    public async Task<IEnumerable<Models.File>> GetFiles(string projectId)
    {
        var listArgs = new ListObjectsArgs()
                .WithBucket(projectId)
                .WithRecursive(true);
        IObservable<Item> observable = this.minio.ListObjectsAsync(listArgs);
        
        var fileList = new List<Models.File>();
        
        IDisposable subscription = observable.Subscribe(
            async (item) => {
                StatObjectArgs statObjectArgs = new StatObjectArgs()
                                                        .WithBucket(projectId)
                                                        .WithObject(item.Key);
                var stat = await this.minio.StatObjectAsync(statObjectArgs);
                
                fileList.Add(new Models.File(
                    item.Key, 
                    item.Size, 
                    item.LastModifiedDateTime,
                    stat.ContentType,
                    item.ETag,
                    new Uri("https://a.now"))
                );
            },
            ex => this.logger.LogError($"🪣 OnError: {ex}"),
            () => this.logger.LogInformation($"🪣 Listed all objects in bucket {projectId}\n"));
        
        observable.Wait();

        return fileList;
    }

    public async Task SetupProject(string projectId)
    {
        logger.LogInformation($"🪣 START SetupProject with {projectId}");

        IDictionary<string, string> tags = new Dictionary<string, string>();
        tags.Add(new KeyValuePair<string, string>("source", "doris-connector"));
        tags.Add(new KeyValuePair<string, string>("projectId", projectId));

        try
        {
            await this.minio.MakeBucketAsync(
                    new MakeBucketArgs()
                        .WithBucket(projectId)
                ).ConfigureAwait(false);

            await this.minio.SetBucketTagsAsync(
                new SetBucketTagsArgs()
                    .WithBucket(projectId)
                    .WithTagging(Tagging.GetBucketTags(tags))
            ).ConfigureAwait(false);

            logger.LogInformation($"🪣 BUCKET {projectId} was created successfully");
        }
        catch (MinioException e)
        {
            logger.LogInformation($"🪣 FAIL BUCET  {e.Message}");
            throw e;
        }
    }
}
