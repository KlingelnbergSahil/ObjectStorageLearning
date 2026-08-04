using ObjectStorage.Backup.Models;

namespace ObjectStorage.Api.Contracts;

public sealed record CreateMongodumpBackupRequest(
    string SourceContainerName,
    string DatabaseName,
    string? MongoUri,
    string? StorageContainer,
    string? StorageKey,
    BackupStrategy? StrategyOverride);
