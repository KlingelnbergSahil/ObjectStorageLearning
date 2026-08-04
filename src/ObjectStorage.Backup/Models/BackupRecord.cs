namespace ObjectStorage.Backup.Models;

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
    bool IsUserUploaded = false,
    bool IsCompressed = true,
    string? OriginalFileName = null);
