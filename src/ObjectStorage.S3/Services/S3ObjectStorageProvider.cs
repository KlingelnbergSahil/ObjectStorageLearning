using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObjectStorage.Core.Abstractions;
using ObjectStorage.Core.Models;
using ObjectStorage.S3.Configuration;

namespace ObjectStorage.S3.Services;

public sealed class S3ObjectStorageProvider : IObjectStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3StorageOptions _options;
    private readonly ILogger<S3ObjectStorageProvider> _logger;

    public S3ObjectStorageProvider(
        IAmazonS3 s3Client,
        IOptions<S3StorageOptions> options,
        ILogger<S3ObjectStorageProvider> logger)
    {
        _s3Client = s3Client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureContainerExistsAsync(
        string container,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(container);

        try
        {
            await _s3Client.GetBucketAclAsync(
                new GetBucketAclRequest
                {
                    BucketName = container
                },
                cancellationToken);
        }
        catch (AmazonS3Exception exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "Creating S3 bucket {BucketName}",
                container);

            await _s3Client.PutBucketAsync(
                new PutBucketRequest
                {
                    BucketName = container
                },
                cancellationToken);
        }
    }

    public async Task UploadAsync(
        StorageObjectId objectId,
        Stream content,
        string contentType,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var request = new PutObjectRequest
        {
            BucketName = objectId.Container,
            Key = objectId.Key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        if (metadata is not null)
        {
            foreach ((string key, string value) in metadata)
            {
                request.Metadata[key] = value;
            }
        }

        await _s3Client.PutObjectAsync(
            request,
            cancellationToken);
    }

    public async Task DownloadToAsync(
        StorageObjectId objectId,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using GetObjectResponse response =
            await _s3Client.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = objectId.Container,
                    Key = objectId.Key
                },
                cancellationToken);

        await response.ResponseStream.CopyToAsync(
            destination,
            cancellationToken);
    }

    public async Task<ObjectMetadata?> GetMetadataAsync(
        StorageObjectId objectId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GetObjectMetadataResponse response =
                await _s3Client.GetObjectMetadataAsync(
                    new GetObjectMetadataRequest
                    {
                        BucketName = objectId.Container,
                        Key = objectId.Key
                    },
                    cancellationToken);

            var customMetadata =
                response.Metadata.Keys.ToDictionary(
                    key => key,
                    key => response.Metadata[key]);

            return new ObjectMetadata(
                objectId.Container,
                objectId.Key,
                response.ContentLength,
                response.Headers.ContentType,
                response.ETag,
                response.LastModified,
                customMetadata);
        }
        catch (AmazonS3Exception exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(
        StorageObjectId objectId,
        CancellationToken cancellationToken = default)
    {
        ObjectMetadata? metadata =
            await GetMetadataAsync(
                objectId,
                cancellationToken);

        return metadata is not null;
    }

    public async Task DeleteAsync(
        StorageObjectId objectId,
        CancellationToken cancellationToken = default)
    {
        await _s3Client.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = objectId.Container,
                Key = objectId.Key
            },
            cancellationToken);
    }

    public Task<TemporaryAccessUrl> CreateUploadUrlAsync(
        StorageObjectId objectId,
        TimeSpan validity,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset expiresAt =
            DateTimeOffset.UtcNow.Add(validity);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = objectId.Container,
            Key = objectId.Key,
            Verb = HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            ContentType = contentType
        };

        using IAmazonS3 presignClient =
            CreatePresignClient();

        string url =
            presignClient.GetPreSignedURL(request);

        var requiredHeaders =
            new Dictionary<string, string>
            {
                ["Content-Type"] = contentType
            };

        return Task.FromResult(
            new TemporaryAccessUrl(
                new Uri(url),
                HttpMethod.Put.Method,
                expiresAt,
                requiredHeaders));
    }

    public Task<TemporaryAccessUrl> CreateDownloadUrlAsync(
        StorageObjectId objectId,
        TimeSpan validity,
        string? downloadFileName = null,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset expiresAt =
            DateTimeOffset.UtcNow.Add(validity);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = objectId.Container,
            Key = objectId.Key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime
        };

        if (!string.IsNullOrWhiteSpace(downloadFileName))
        {
            request.ResponseHeaderOverrides.ContentDisposition =
                $"attachment; filename=\"{SanitizeFileName(downloadFileName)}\"";
        }

        using IAmazonS3 presignClient =
            CreatePresignClient();

        string url =
            presignClient.GetPreSignedURL(request);

        return Task.FromResult(
            new TemporaryAccessUrl(
                new Uri(url),
                HttpMethod.Get.Method,
                expiresAt,
                new Dictionary<string, string>()));
    }

    private static string SanitizeFileName(string fileName)
    {
        return string.Concat(
            fileName.Where(
                character =>
                    !Path.GetInvalidFileNameChars().Contains(character) &&
                    character != '"' &&
                    character != '\r' &&
                    character != '\n'));
    }

    private IAmazonS3 CreatePresignClient()
    {
        string serviceUrl =
            string.IsNullOrWhiteSpace(_options.PublicServiceUrl)
                ? _options.ServiceUrl
                : _options.PublicServiceUrl;

        var credentials =
            new BasicAWSCredentials(
                _options.AccessKey,
                _options.SecretKey);

        var clientConfiguration =
            new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = _options.ForcePathStyle,
                AuthenticationRegion = _options.Region,
                UseHttp = _options.UseHttp
            };

        return new AmazonS3Client(
            credentials,
            clientConfiguration);
    }
}
