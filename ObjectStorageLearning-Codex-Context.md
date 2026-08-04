Object Storage Learning Project — Codex Context and Implementation Plan

1. Purpose

This project is a focused learning environment for designing object storage support for GearEngine.

The current scope is intentionally limited to:

On-premises object storage using SeaweedFS through its S3-compatible API.

Cloud object storage using Azure Blob Storage.

A provider-neutral C# abstraction.

SeaweedFS integration through AWSSDK.S3.

Azure Blob integration through Azure.Storage.Blobs.

Direct browser upload and download using:

S3 presigned URLs for SeaweedFS.

SAS URLs for Azure Blob Storage.

Basic object operations:

Create container or bucket.

Upload.

Download.

Read metadata.

Check existence.

Delete.

Generate temporary upload URLs.

Generate temporary download URLs.

The learning project is separate from GearEngine. Once the design is proven, the same structure can later be extracted into GearEngine modules.

2. Explicitly excluded from the first phase

Do not implement or add the following yet:

MinIO.

Amazon S3 account integration.

Garage.

MongoDB.

Percona Server for MongoDB.

Percona Backup for MongoDB.

mongodump.

mongorestore.

Backup and restore workflows.

Hangfire.

ABP permissions.

GearEngine integration.

Traefik.

Multipart upload.

Resumable upload.

Upload progress persistence.

Database persistence.

Background jobs.

Multiple active providers at the same time.

Production authentication.

Retention policies.

Replication.

Object lock.

Encryption-key management.

The goal is first to understand the two storage models clearly:

SeaweedFS
    ↓
S3-compatible API
    ↓
AWSSDK.S3
    ↓
S3 presigned URLs

Azure Blob Storage
    ↓
Azure.Storage.Blobs
    ↓
SAS URLs

3. Current solution

Expected root structure:

ObjectStorageLearning/
├── docker/
│   └── seaweedfs/
│       ├── compose.yml
│       └── s3-config.json
│
├── src/
│   ├── ObjectStorage.Api/
│   │   ├── Controllers/
│   │   │   └── StorageController.cs
│   │   ├── Contracts/
│   │   │   ├── CreateDownloadUrlRequest.cs
│   │   │   └── CreateUploadUrlRequest.cs
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   │
│   ├── ObjectStorage.Core/
│   │   ├── Abstractions/
│   │   │   └── IObjectStorageProvider.cs
│   │   └── Models/
│   │       ├── ObjectMetadata.cs
│   │       ├── StorageObjectId.cs
│   │       └── TemporaryAccessUrl.cs
│   │
│   ├── ObjectStorage.S3/
│   │   ├── Configuration/
│   │   │   └── S3StorageOptions.cs
│   │   ├── DependencyInjection/
│   │   │   └── S3StorageServiceCollectionExtensions.cs
│   │   └── Services/
│   │       └── S3ObjectStorageProvider.cs
│   │
│   └── ObjectStorage.AzureBlob/
│       ├── Configuration/
│       │   └── AzureBlobStorageOptions.cs
│       ├── DependencyInjection/
│       │   └── AzureBlobServiceCollectionExtensions.cs
│       └── Services/
│           └── AzureBlobObjectStorageProvider.cs
│
├── ObjectStorageLearning.slnx
└── README.md

If the solution uses .sln instead of .slnx, keep the existing solution format.

4. Files and folders to remove or exclude

Remove these if they currently exist:

docker/minio/
ObjectStorage.Core/Exceptions/ObjectStorageException.cs
ObjectStorage.Api/Contracts/ObjectMetadataResponse.cs
ObjectStorage.Api/Contracts/TemporaryAccessUrlResponse.cs

The API may return ObjectMetadata and TemporaryAccessUrl directly during the learning phase.

Remove Directory.Build.props only if it is unused.

Keep it only when it contains shared settings such as:

<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>

The integration test project may remain in the solution, but it does not need to be implemented in the first iteration.

Remove generated placeholder files when replacing them:

ObjectStorage.Core/Class1.cs
ObjectStorage.S3/Class1.cs
ObjectStorage.AzureBlob/Class1.cs
ObjectStorage.IntegrationTests/UnitTest1.cs

5. Required project references

The dependency direction must remain:

ObjectStorage.Api
├── ObjectStorage.Core
├── ObjectStorage.S3
└── ObjectStorage.AzureBlob

ObjectStorage.S3
└── ObjectStorage.Core

ObjectStorage.AzureBlob
└── ObjectStorage.Core

ObjectStorage.Core must not reference infrastructure projects.

Commands:

dotnet add .\src\ObjectStorage.Api\ObjectStorage.Api.csproj reference .\src\ObjectStorage.Core\ObjectStorage.Core.csproj
dotnet add .\src\ObjectStorage.Api\ObjectStorage.Api.csproj reference .\src\ObjectStorage.S3\ObjectStorage.S3.csproj
dotnet add .\src\ObjectStorage.Api\ObjectStorage.Api.csproj reference .\src\ObjectStorage.AzureBlob\ObjectStorage.AzureBlob.csproj

dotnet add .\src\ObjectStorage.S3\ObjectStorage.S3.csproj reference .\src\ObjectStorage.Core\ObjectStorage.Core.csproj
dotnet add .\src\ObjectStorage.AzureBlob\ObjectStorage.AzureBlob.csproj reference .\src\ObjectStorage.Core\ObjectStorage.Core.csproj

6. Required NuGet packages

ObjectStorage.S3

dotnet add .\src\ObjectStorage.S3\ObjectStorage.S3.csproj package AWSSDK.S3
dotnet add .\src\ObjectStorage.S3\ObjectStorage.S3.csproj package Microsoft.Extensions.Options.ConfigurationExtensions

ObjectStorage.AzureBlob

dotnet add .\src\ObjectStorage.AzureBlob\ObjectStorage.AzureBlob.csproj package Azure.Storage.Blobs
dotnet add .\src\ObjectStorage.AzureBlob\ObjectStorage.AzureBlob.csproj package Azure.Identity
dotnet add .\src\ObjectStorage.AzureBlob\ObjectStorage.AzureBlob.csproj package Microsoft.Extensions.Options.ConfigurationExtensions

Then run:

dotnet restore
dotnet build

7. Target architecture

                     ObjectStorage.Api
                            |
                 IObjectStorageProvider
                            |
             ┌──────────────┴──────────────┐
             |                             |
  S3ObjectStorageProvider       AzureBlobStorageProvider
             |                             |
       AWSSDK.S3                 Azure.Storage.Blobs
             |                             |
        SeaweedFS                   Azure Blob Storage

Important:

Do not create SeaweedFSObjectStorageProvider.

SeaweedFS is a configuration of the S3 provider.

Azure Blob requires a separate implementation because it does not use the S3 API.

Do not expose IAmazonS3 or BlobServiceClient directly from controllers.

8. Provider-neutral models

StorageObjectId

namespace ObjectStorage.Core.Models;

public sealed record StorageObjectId(
    string Container,
    string Key)
{
    public static StorageObjectId Create(
        string container,
        string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(container);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new StorageObjectId(
            container.Trim(),
            key.Replace('\\', '/').TrimStart('/'));
    }
}

Meaning:

Container
├── S3: bucket
└── Azure: blob container

Key
├── S3: object key
└── Azure: blob name

ObjectMetadata

namespace ObjectStorage.Core.Models;

public sealed record ObjectMetadata(
    string Container,
    string Key,
    long Size,
    string? ContentType,
    string? ETag,
    DateTimeOffset? LastModified,
    IReadOnlyDictionary<string, string> CustomMetadata);

TemporaryAccessUrl

namespace ObjectStorage.Core.Models;

public sealed record TemporaryAccessUrl(
    Uri Url,
    HttpMethod Method,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);

RequiredHeaders is important for signed uploads.

If Content-Type is included in a signature, the client must send the same value.

9. Provider-neutral interface

Create:

using ObjectStorage.Core.Models;

namespace ObjectStorage.Core.Abstractions;

public interface IObjectStorageProvider
{
    Task EnsureContainerExistsAsync(
        string container,
        CancellationToken cancellationToken = default);

    Task UploadAsync(
        StorageObjectId objectId,
        Stream content,
        string contentType,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task DownloadToAsync(
        StorageObjectId objectId,
        Stream destination,
        CancellationToken cancellationToken = default);

    Task<ObjectMetadata?> GetMetadataAsync(
        StorageObjectId objectId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        StorageObjectId objectId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        StorageObjectId objectId,
        CancellationToken cancellationToken = default);

    Task<TemporaryAccessUrl> CreateUploadUrlAsync(
        StorageObjectId objectId,
        TimeSpan validity,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<TemporaryAccessUrl> CreateDownloadUrlAsync(
        StorageObjectId objectId,
        TimeSpan validity,
        string? downloadFileName = null,
        CancellationToken cancellationToken = default);
}

Do not add multipart-specific methods yet.

10. SeaweedFS Docker environment

Create:

docker/seaweedfs/compose.yml
docker/seaweedfs/s3-config.json

docker/seaweedfs/compose.yml

name: object-storage-learning

services:
  seaweed-master:
    image: chrislusf/seaweedfs:latest
    container_name: object-storage-seaweed-master
    command:
      - master
      - -ip=seaweed-master
      - -mdir=/data
      - -volumeSizeLimitMB=1024
    ports:
      - "9333:9333"
    volumes:
      - seaweed-master-data:/data
    restart: unless-stopped
    networks:
      - storage-learning

  seaweed-volume:
    image: chrislusf/seaweedfs:latest
    container_name: object-storage-seaweed-volume
    command:
      - volume
      - -mserver=seaweed-master:9333
      - -ip=seaweed-volume
      - -port=8080
      - -dir=/data
      - -max=10
    depends_on:
      - seaweed-master
    ports:
      - "8080:8080"
    volumes:
      - seaweed-volume-data:/data
    restart: unless-stopped
    networks:
      - storage-learning

  seaweed-filer:
    image: chrislusf/seaweedfs:latest
    container_name: object-storage-seaweed-filer
    command:
      - filer
      - -master=seaweed-master:9333
      - -ip=seaweed-filer
    depends_on:
      - seaweed-master
      - seaweed-volume
    ports:
      - "8888:8888"
    volumes:
      - seaweed-filer-data:/data
    restart: unless-stopped
    networks:
      - storage-learning

  seaweed-s3:
    image: chrislusf/seaweedfs:latest
    container_name: object-storage-seaweed-s3
    command:
      - s3
      - -filer=seaweed-filer:8888
      - -port=8333
      - -config=/etc/seaweedfs/s3-config.json
    depends_on:
      - seaweed-filer
    ports:
      - "8333:8333"
    volumes:
      - ./s3-config.json:/etc/seaweedfs/s3-config.json:ro
    restart: unless-stopped
    networks:
      - storage-learning

networks:
  storage-learning:
    driver: bridge

volumes:
  seaweed-master-data:
  seaweed-volume-data:
  seaweed-filer-data:

docker/seaweedfs/s3-config.json

{
  "identities": [
    {
      "name": "gearengine-learning",
      "credentials": [
        {
          "accessKey": "gearengine-access-key",
          "secretKey": "gearengine-secret-key-change-me"
        }
      ],
      "actions": [
        "Admin",
        "Read",
        "Write",
        "List",
        "Tagging"
      ]
    }
  ]
}

The credentials are for local development only.

Start SeaweedFS:

docker compose -f .\docker\seaweedfs\compose.yml up -d

Validate:

docker compose -f .\docker\seaweedfs\compose.yml ps
docker compose -f .\docker\seaweedfs\compose.yml logs seaweed-s3

Endpoints:

Master UI: http://localhost:9333
Filer UI:  http://localhost:8888
S3 API:    http://localhost:8333

11. S3 configuration

Create:

namespace ObjectStorage.S3.Configuration;

public sealed class S3StorageOptions
{
    public const string SectionName = "ObjectStorage:S3";

    public string ServiceUrl { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public bool ForcePathStyle { get; set; } = true;
    public bool UseHttp { get; set; }
    public int PresignedUrlExpiryMinutes { get; set; } = 15;
}

Initial SeaweedFS configuration:

{
  "ObjectStorage": {
    "Provider": "S3",
    "DefaultContainer": "ge-files",
    "S3": {
      "ServiceUrl": "http://localhost:8333",
      "AccessKey": "gearengine-access-key",
      "SecretKey": "gearengine-secret-key-change-me",
      "Region": "us-east-1",
      "ForcePathStyle": true,
      "UseHttp": true,
      "PresignedUrlExpiryMinutes": 15
    }
  }
}

Use user secrets or environment variables for real credentials.

12. S3 implementation requirements

Implement S3ObjectStorageProvider using IAmazonS3.

Required operations:

Ensure bucket exists
PutObject
GetObject
HeadObject
DeleteObject
Generate presigned PUT
Generate presigned GET

Requirements:

Use path-style URLs for SeaweedFS.

Do not pass storage credentials to clients.

Use short-lived presigned URLs.

Preserve required signed headers.

Return null for missing object metadata.

Use structured logging.

Do not add SeaweedFS-specific behavior unless compatibility testing proves it is necessary.

13. Azure Blob configuration

Create:

namespace ObjectStorage.AzureBlob.Configuration;

public sealed class AzureBlobStorageOptions
{
    public const string SectionName = "ObjectStorage:AzureBlob";

    public string ServiceUri { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string DefaultContainer { get; set; } = "ge-files";
    public int SasExpiryMinutes { get; set; } = 15;
}

Learning configuration:

{
  "ObjectStorage": {
    "Provider": "AzureBlob",
    "DefaultContainer": "ge-files",
    "AzureBlob": {
      "ServiceUri": "https://your-account.blob.core.windows.net",
      "ConnectionString": "",
      "DefaultContainer": "ge-files",
      "SasExpiryMinutes": 15
    }
  }
}

For the first working implementation, connection-string authentication is acceptable for local testing.

Later prefer:

DefaultAzureCredential
Managed Identity
User delegation SAS

Do not hardcode Azure credentials.

14. Azure implementation requirements

Implement AzureBlobObjectStorageProvider using BlobServiceClient.

Required mappings:

Ensure container exists → CreateIfNotExistsAsync
Upload                  → BlobClient.UploadAsync
Download                → BlobClient.DownloadStreamingAsync
Metadata                → BlobClient.GetPropertiesAsync
Delete                  → BlobClient.DeleteIfExistsAsync
Temporary upload        → SAS URL with write/create permission
Temporary download      → SAS URL with read permission

The controller should receive the same TemporaryAccessUrl model regardless of provider.

Do not expose Azure SDK types from the API.

15. Provider registration

Program.cs should select one provider from configuration:

string provider =
    builder.Configuration["ObjectStorage:Provider"]
    ?? throw new InvalidOperationException(
        "ObjectStorage:Provider is not configured.");

switch (provider)
{
    case "S3":
        builder.Services.AddS3ObjectStorage(
            builder.Configuration);
        break;

    case "AzureBlob":
        builder.Services.AddAzureBlobObjectStorage(
            builder.Configuration);
        break;

    default:
        throw new InvalidOperationException(
            $"Unsupported object-storage provider: {provider}");
}

Only one provider needs to be active at startup in the first version.

Do not implement named providers or runtime provider switching yet.

16. API request contracts

CreateUploadUrlRequest

namespace ObjectStorage.Api.Contracts;

public sealed record CreateUploadUrlRequest(
    string Container,
    string ObjectKey,
    string ContentType,
    int ExpiryMinutes = 15);

CreateDownloadUrlRequest

namespace ObjectStorage.Api.Contracts;

public sealed record CreateDownloadUrlRequest(
    string Container,
    string ObjectKey,
    string? DownloadFileName,
    int ExpiryMinutes = 15);

The controller may return core models directly during the learning phase.

17. First API endpoints

Create StorageController with:

POST   /api/storage/containers/{container}
POST   /api/storage/server-upload
POST   /api/storage/upload-url
POST   /api/storage/download-url
GET    /api/storage/metadata
DELETE /api/storage

Rules:

server-upload is only for learning and small files.

Direct upload URLs should be used to prove browser-to-storage transfer.

Direct download URLs should be used to prove storage-to-browser transfer.

The API should not proxy large files in the final design.

Signed URLs must not be stored.

The provider credentials must remain only on the server.

18. Presigned URL and SAS study

The project must demonstrate the common concept:

GearEngine/API
    ↓ checks authorization
    ↓ uses permanent provider credentials
    ↓ creates temporary scoped URL
Client
    ↓ uploads or downloads directly
Storage provider

SeaweedFS

Use:

Presigned PUT
Presigned GET

Azure Blob

Use:

SAS upload URL
SAS download URL

Required manual tests:

Create a bucket or container.

Generate temporary upload URL.

Upload a file directly.

Verify object metadata.

Generate temporary download URL.

Download the file directly.

Confirm the bytes match.

Delete the object.

Confirm the object no longer exists.

Test expired URL behavior.

Test changed signed header behavior for S3.

Confirm that file bytes are not flowing through the API during direct transfer.

19. First implementation milestones

Milestone 1 — compile-ready structure

Remove excluded files.

Remove generated placeholder classes.

Add project references.

Add required NuGet packages.

Add core models.

Add provider-neutral interface.

Add S3 options.

Add Azure options.

Add dependency injection extensions.

Ensure dotnet build succeeds.

Milestone 2 — SeaweedFS

Add SeaweedFS Compose file.

Add S3 credentials file.

Start SeaweedFS.

Verify the S3 endpoint manually.

Implement S3 object operations.

Implement presigned PUT and GET.

Add Swagger endpoints.

Milestone 3 — Azure Blob

Implement Azure Blob provider.

Add SAS upload and download generation.

Configure one Azure Storage account.

Repeat the same API tests.

Milestone 4 — compare providers

Document differences in:

Configuration
Authentication
Container creation
Upload behavior
Download behavior
Metadata
Temporary access URLs
Expiration
Required headers
Error responses

Milestone 5 — later only

After the above works, consider:

S3 multipart upload
Azure block uploads
Progress
Retry
Resume
Integration tests
GearEngine extraction

20. First Codex task

Implement only Milestone 1 and Milestone 2.

Expected output:

Simplified solution structure.

No MinIO files.

No MongoDB or Percona files.

No backup/restore code.

Compile-ready provider-neutral core.

Compile-ready S3 project.

Azure project containing configuration and DI structure, but Azure implementation may be deferred if needed.

SeaweedFS Docker Compose.

SeaweedFS S3 credentials file.

Storage API endpoints for basic S3 operations.

Presigned PUT and GET support.

Swagger instructions.

dotnet build executed.

docker compose config executed.

Honest report of anything not verified.

Do not implement:

Multipart upload
Hangfire
Authentication
MongoDB
Percona
PBM
MinIO
AWS account integration
GearEngine integration

21. Suggested Codex prompt

Read ObjectStorageLearning-Codex-Context.md completely.

Inspect the existing solution before modifying anything.

Implement only:
- Milestone 1 — compile-ready structure
- Milestone 2 — SeaweedFS

The current scope is:
- SeaweedFS on-prem through AWSSDK.S3
- Azure Blob later through Azure.Storage.Blobs

Remove or exclude:
- MinIO
- MongoDB
- Percona
- PBM
- backup/restore
- Hangfire
- multipart upload
- authentication
- GearEngine integration

After implementation:
- run dotnet build
- run docker compose config
- summarize all files changed
- report anything that could not be verified

22. Definition of done for the first iteration

The first iteration is complete when:

dotnet build

succeeds, and:

docker compose -f .\docker\seaweedfs\compose.yml up -d

starts SeaweedFS successfully, and the API can:

create bucket
upload object
read metadata
generate presigned PUT
generate presigned GET
download object
delete object

against:

http://localhost:8333

Azure Blob implementation is the next milestone after the SeaweedFS flow is stable.