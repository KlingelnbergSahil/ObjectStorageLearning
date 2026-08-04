using System.Text.Json.Serialization;

namespace ObjectStorage.Blazor.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackupStrategy
{
    Mongodump,
    PbmLogical,
    PbmPhysical
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackupStatus
{
    Running,
    Completed,
    Failed
}

public sealed record BackupRecord(
    string Id,
    string DatabaseName,
    BackupStrategy Strategy,
    BackupStatus Status,
    string StorageContainer,
    string StorageKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    long? SizeBytes,
    string? SourceContainerName,
    string? TargetContainerName,
    string? ToolOutput,
    string? Error,
    bool IsUserUploaded,
    bool IsCompressed,
    string? OriginalFileName);

public sealed record DatabaseSizeInfo(
    string ContainerName,
    string DatabaseName,
    long DataSizeBytes,
    long StorageSizeBytes,
    long IndexSizeBytes,
    BackupStrategy SelectedStrategy);

public sealed record MongoDatabaseInfo(
    string Name,
    long SizeOnDiskBytes,
    bool Empty);

public sealed record MongoContainerInfo(
    string Name,
    string Image,
    string Status,
    string Ports);

public sealed record TemporaryAccessUrl(
    Uri Url,
    string HttpMethod,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);

public sealed record CommandResultResponse(
    int ExitCode,
    bool Succeeded,
    string StandardOutput,
    string StandardError);

public sealed record CreateMongodumpBackupRequest(
    string SourceContainerName,
    string DatabaseName,
    string? MongoUri,
    string? StorageContainer,
    string? StorageKey,
    BackupStrategy? StrategyOverride);

public sealed record CreateBackupUploadUrlRequest(
    string StorageContainer,
    string StorageKey,
    string ContentType = "application/gzip",
    int ExpiryMinutes = 15);

public sealed record CreateBackupDownloadUrlRequest(
    string StorageContainer,
    string StorageKey,
    string? DownloadFileName,
    int ExpiryMinutes = 15);

public sealed record RegisterUploadedBackupRequest(
    string DatabaseName,
    string StorageContainer,
    string StorageKey,
    bool IsCompressed,
    string? OriginalFileName = null,
    BackupStrategy Strategy = BackupStrategy.Mongodump);

public sealed record RestoreBackupRecordRequest(
    string TargetContainerName,
    string? MongoUri,
    string? SourceDatabaseName = null,
    string? TargetDatabaseName = null,
    bool? IsCompressed = null,
    bool DropExisting = true);

public sealed record CreatePbmBackupRequest(
    BackupStrategy Strategy);

public sealed record CreatePbmRestoreRequest(
    string BackupName,
    bool DropExistingData = false,
    string? DatabaseNameToDrop = null);

public sealed record PbmSnapshot(
    string Name,
    string Size,
    string Type,
    string Status);

public sealed record TimingEntry(
    DateTimeOffset StartedAt,
    string Action,
    TimeSpan Duration,
    bool Succeeded);
