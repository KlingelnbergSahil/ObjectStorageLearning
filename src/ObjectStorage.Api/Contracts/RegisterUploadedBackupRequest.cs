using ObjectStorage.Backup.Models;

namespace ObjectStorage.Api.Contracts;

public sealed record RegisterUploadedBackupRequest(
    string DatabaseName,
    string StorageContainer,
    string StorageKey,
    bool IsCompressed,
    string? OriginalFileName = null,
    BackupStrategy Strategy = BackupStrategy.Mongodump);
