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
using Models;

public class S3StorageService : IStorageService
{
    private readonly ILogger logger;
    public IMinioClient minio;
    public S3StorageService(IMinioClient minio, ILogger logger)
    {
        this.logger = logger;
        this.minio = minio;
    }
    public async Task AddFile(string projectId, string fileName, string contentType, Stream stream)
    {
        var args = new PutObjectArgs()
            .WithBucket(projectId)
            .WithObject(fileName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType);
            
        await minio.PutObjectAsync(args).ConfigureAwait(false);
    }

    public async Task<bool> ProjectExists(string projectId){
        return await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(projectId));
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<RoFile>> GetFiles(string projectId)
    {
        var listArgs = new ListObjectsArgs()
                .WithBucket(projectId)
                .WithRecursive(true);
        IObservable<Item> observable = minio.ListObjectsAsync(listArgs);
        
        var fileList = new List<RoFile>();
        
        IDisposable subscription = observable.Subscribe(
            async (item) => {
                StatObjectArgs statObjectArgs = new StatObjectArgs()
                                                        .WithBucket(projectId)
                                                        .WithObject(item.Key);
                var stat = await minio.StatObjectAsync(statObjectArgs);
                
                fileList.Add(new RoFile(
                    item.Key, 
                    item.Size.ToString(), 
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
            await minio.MakeBucketAsync(
                    new MakeBucketArgs()
                        .WithBucket(projectId)
                ).ConfigureAwait(false);

            await minio.SetBucketTagsAsync(
                new SetBucketTagsArgs()
                    .WithBucket(projectId)
                    .WithTagging(Tagging.GetBucketTags(tags))
            ).ConfigureAwait(false);

            logger.LogInformation($"🪣 BUCKET {projectId} was created successfully");
        }
        catch (MinioException e)
        {
            logger.LogInformation($"🪣 FAIL BUCKET  {e.Message}");
            throw e;
        }
    }
}
