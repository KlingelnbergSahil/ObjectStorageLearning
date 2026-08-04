using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ObjectStorage.Api.Contracts;
using ObjectStorage.Backup.Configuration;
using ObjectStorage.Backup.Models;
using ObjectStorage.Backup.Services;
using ObjectStorage.Core.Abstractions;
using ObjectStorage.Core.Models;

namespace ObjectStorage.Api.Controllers;

[ApiController]
[Route("api/backup")]
public sealed class BackupController : ControllerBase
{
    private readonly MongoInspector _mongoInspector;
    private readonly MongodumpBackupRunner _mongodumpBackupRunner;
    private readonly MongorestoreRunner _mongorestoreRunner;
    private readonly PbmRunner _pbmRunner;
    private readonly BackupCatalog _catalog;
    private readonly IObjectStorageProvider _storage;
    private readonly IObjectStoragePrefixArchiveProvider _prefixArchive;
    private readonly BackupOptions _backupOptions;

    public BackupController(
        MongoInspector mongoInspector,
        MongodumpBackupRunner mongodumpBackupRunner,
        MongorestoreRunner mongorestoreRunner,
        PbmRunner pbmRunner,
        BackupCatalog catalog,
        IObjectStorageProvider storage,
        IObjectStoragePrefixArchiveProvider prefixArchive,
        IOptions<BackupOptions> backupOptions)
    {
        _mongoInspector = mongoInspector;
        _mongodumpBackupRunner = mongodumpBackupRunner;
        _mongorestoreRunner = mongorestoreRunner;
        _pbmRunner = pbmRunner;
        _catalog = catalog;
        _storage = storage;
        _prefixArchive = prefixArchive;
        _backupOptions = backupOptions.Value;
    }

    [HttpGet("mongo-containers")]
    public async Task<IReadOnlyList<MongoContainerInfo>> ListMongoContainersAsync(
        CancellationToken cancellationToken)
    {
        return await _mongoInspector.ListMongoContainersAsync(
            cancellationToken);
    }

    [HttpGet("mongo-containers/{containerName}/databases")]
    public async Task<IReadOnlyList<MongoDatabaseInfo>> ListDatabasesAsync(
        string containerName,
        CancellationToken cancellationToken)
    {
        return await _mongoInspector.ListDatabasesAsync(
            containerName,
            cancellationToken);
    }

    [HttpGet("mongo-containers/{containerName}/databases/{databaseName}/size")]
    public async Task<DatabaseSizeInfo> GetDatabaseSizeAsync(
        string containerName,
        string databaseName,
        CancellationToken cancellationToken)
    {
        return await _mongoInspector.GetDatabaseSizeAsync(
            containerName,
            databaseName,
            cancellationToken);
    }

    [HttpGet("records")]
    public async Task<IReadOnlyList<BackupRecord>> ListRecordsAsync(
        CancellationToken cancellationToken)
    {
        return await _catalog.ListAsync(
            cancellationToken);
    }

    [HttpPost("records/uploaded")]
    public async Task<ActionResult<BackupRecord>> RegisterUploadedBackupAsync(
        [FromBody] RegisterUploadedBackupRequest request,
        CancellationToken cancellationToken)
    {
        string id =
            $"uploaded-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{request.DatabaseName}";

        var record =
            new BackupRecord(
                id,
                request.DatabaseName,
                request.Strategy,
                BackupStatus.Completed,
                request.StorageContainer,
                request.StorageKey,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                "User uploaded backup file.",
                null,
                IsUserUploaded: true,
                IsCompressed: request.IsCompressed,
                OriginalFileName: request.OriginalFileName);

        await _catalog.UpsertAsync(
            record,
            cancellationToken);

        return Ok(record);
    }

    [HttpPost("records/{recordId}/restore")]
    public async Task<ActionResult<object>> RestoreRecordAsync(
        string recordId,
        [FromBody] RestoreBackupRecordRequest request,
        CancellationToken cancellationToken)
    {
        BackupRecord? record =
            await _catalog.GetAsync(
                recordId,
                cancellationToken);

        if (record is null)
        {
            return NotFound();
        }

        string output =
            await _mongorestoreRunner.RestoreAsync(
                request.TargetContainerName,
                request.MongoUri,
                record.StorageContainer,
                record.StorageKey,
                request.SourceDatabaseName,
                request.TargetDatabaseName,
                request.IsCompressed ?? record.IsCompressed,
                request.DropExisting,
                cancellationToken);

        return Ok(
            new
            {
                RecordId = record.Id,
                record.StorageContainer,
                record.StorageKey,
                Output = output
            });
    }

    [HttpPost("mongodump")]
    public async Task<ActionResult<BackupRecord>> CreateMongodumpBackupAsync(
        [FromBody] CreateMongodumpBackupRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StrategyOverride is not null &&
            request.StrategyOverride != BackupStrategy.Mongodump)
        {
            return BadRequest(
                "Use /api/backup/pbm/backup for PBM logical or physical backups.");
        }

        BackupRecord record =
            await _mongodumpBackupRunner.BackupAsync(
                request.SourceContainerName,
                request.DatabaseName,
                request.MongoUri,
                request.StorageContainer,
                request.StorageKey,
                cancellationToken);

        return Ok(record);
    }

    [HttpPost("mongorestore")]
    public async Task<ActionResult<object>> RestoreMongodumpBackupAsync(
        [FromBody] CreateMongorestoreRequest request,
        CancellationToken cancellationToken)
    {
        string output =
            await _mongorestoreRunner.RestoreAsync(
                request.TargetContainerName,
                request.MongoUri,
                request.StorageContainer,
                request.StorageKey,
                request.SourceDatabaseName,
                request.TargetDatabaseName,
                request.IsCompressed,
                request.DropExisting,
                cancellationToken);

        return Ok(
            new
            {
                request.TargetContainerName,
                request.StorageContainer,
                request.StorageKey,
                Output = output
            });
    }

    [HttpPost("download-url")]
    public async Task<ActionResult<TemporaryAccessUrl>> CreateDownloadUrlAsync(
        [FromBody] CreateBackupDownloadUrlRequest request,
        CancellationToken cancellationToken)
    {
        TemporaryAccessUrl url =
            await _storage.CreateDownloadUrlAsync(
                StorageObjectId.Create(
                    request.StorageContainer,
                    request.StorageKey),
                ValidateExpiry(request.ExpiryMinutes),
                request.DownloadFileName,
                cancellationToken);

        return Ok(url);
    }

    [HttpPost("upload-url")]
    public async Task<ActionResult<TemporaryAccessUrl>> CreateUploadUrlAsync(
        [FromBody] CreateBackupUploadUrlRequest request,
        CancellationToken cancellationToken)
    {
        await _storage.EnsureContainerExistsAsync(
            request.StorageContainer,
            cancellationToken);

        TemporaryAccessUrl url =
            await _storage.CreateUploadUrlAsync(
                StorageObjectId.Create(
                    request.StorageContainer,
                    request.StorageKey),
                ValidateExpiry(request.ExpiryMinutes),
                request.ContentType,
                cancellationToken);

        return Ok(url);
    }

    [HttpPost("pbm/configure")]
    public async Task<ActionResult<CommandResultResponse>> ConfigurePbmAsync(
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await _pbmRunner.ConfigureAsync(
                cancellationToken));
    }

    [HttpGet("pbm/status")]
    public async Task<ActionResult<CommandResultResponse>> GetPbmStatusAsync(
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await _pbmRunner.StatusAsync(
                cancellationToken));
    }

    [HttpGet("pbm/list")]
    public async Task<ActionResult<CommandResultResponse>> ListPbmBackupsAsync(
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await _pbmRunner.ListAsync(
                cancellationToken));
    }

    [HttpGet("pbm/logs")]
    public async Task<ActionResult<CommandResultResponse>> GetPbmLogsAsync(
        [FromQuery] int tail,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await _pbmRunner.LogsAsync(
                tail,
                cancellationToken));
    }

    [HttpPost("pbm/backup")]
    public async Task<ActionResult<CommandResultResponse>> CreatePbmBackupAsync(
        [FromBody] CreatePbmBackupRequest request,
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await _pbmRunner.BackupAsync(
                request.Strategy,
                cancellationToken));
    }

    [HttpPost("pbm/restore")]
    public async Task<ActionResult<CommandResultResponse>> RestorePbmBackupAsync(
        [FromBody] CreatePbmRestoreRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DropExistingData)
        {
            string databaseName =
                string.IsNullOrWhiteSpace(request.DatabaseNameToDrop)
                    ? "R300"
                    : request.DatabaseNameToDrop;

            BackupCommandResult dropResult =
                await _pbmRunner.DropDatabaseAsync(
                    databaseName,
                    cancellationToken);

            if (!dropResult.Succeeded)
            {
                return ToResponse(dropResult);
            }
        }

        return ToResponse(
            await _pbmRunner.RestoreAsync(
                request.BackupName,
                cancellationToken));
    }

    [HttpGet("pbm/snapshots/{backupName}/download")]
    public async Task DownloadPbmSnapshotAsync(
        string backupName,
        CancellationToken cancellationToken)
    {
        if (backupName.Any(char.IsControl) ||
            backupName.Contains("..", StringComparison.Ordinal) ||
            backupName.Contains('/', StringComparison.Ordinal) ||
            backupName.Contains('\\', StringComparison.Ordinal))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync(
                "Invalid PBM backup name.",
                cancellationToken);
            return;
        }

        string fileName =
            $"{backupName}.pbm-snapshot.zip";

        Response.ContentType = "application/zip";
        Response.Headers[HeaderNames.ContentDisposition] =
            $"attachment; filename=\"{fileName}\"";

        await _prefixArchive.StreamPrefixAsZipAsync(
            _backupOptions.PbmBackupContainer,
            $"pbm/{backupName}",
            Response.Body,
            cancellationToken);
    }

    [HttpPost("pbm/resync")]
    public async Task<ActionResult<CommandResultResponse>> ResyncPbmAsync(
        CancellationToken cancellationToken)
    {
        return ToResponse(
            await _pbmRunner.ResyncAsync(
                cancellationToken));
    }

    private static CommandResultResponse ToResponse(
        BackupCommandResult result)
    {
        return new CommandResultResponse(
            result.ExitCode,
            result.Succeeded,
            result.StandardOutput,
            result.StandardError);
    }

    private static TimeSpan ValidateExpiry(
        int expiryMinutes)
    {
        if (expiryMinutes is < 1 or > 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiryMinutes),
                "Expiry must be between 1 and 60 minutes.");
        }

        return TimeSpan.FromMinutes(expiryMinutes);
    }
}
