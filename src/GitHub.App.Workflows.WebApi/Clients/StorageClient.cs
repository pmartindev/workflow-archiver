using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Minio;

namespace GitHub.App.Workflows.WebApi.Clients;

public class StorageClient : IStorageClient
{
    private readonly IMinioClient _minioClient;

    public string Endpoint { get; }

    public StorageClient(IConfiguration configuration)
    {
        var accessKeyId = configuration.GetSection("S3Bucket:AccessKeyId").Value;

        if (accessKeyId == null) throw new NullReferenceException("Unable to locate the Access Key ID for the S3 bucket in the configuration.");

        var accessKeySecret = configuration.GetSection("S3Bucket:AccessKeySecret").Value;

        if (accessKeySecret == null) throw new NullReferenceException("Unable to locate the Access Key Secret for the S3 bucket in the configuration.");

        var endpoint = configuration.GetSection("S3Bucket:EndPoint").Value;

        Endpoint = endpoint ?? throw new NullReferenceException("Unable to locate the S3 endpoint in the configuration.");

        var useSsl = configuration.GetSection("S3Bucket:UseSsl").Value == true.ToString();

        _minioClient = new MinioClient()
            .WithEndpoint(Endpoint)
            .WithCredentials(accessKeyId, accessKeySecret)
            .WithSSL(useSsl)
            .Build();
    }

    private async Task EnsureLocationExists(string location)
    {
        var bucketExistsArgs = new BucketExistsArgs().WithBucket(location);

        var found = await _minioClient.BucketExistsAsync(bucketExistsArgs);

        if (!found)
        {
            var mbArgs = new MakeBucketArgs().WithBucket(location);

            await _minioClient.MakeBucketAsync(mbArgs);
        }
    }

    public async Task UploadFile(string location, string fileName, byte[] buffer)
    {
        await EnsureLocationExists(location);

        var fileMemoryStream = new MemoryStream(buffer);

        var contentType = "application/zip";

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(location)
            .WithObject(fileName.ToLower())
            .WithStreamData(fileMemoryStream)
            .WithObjectSize(fileMemoryStream.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putObjectArgs);
    }
}
