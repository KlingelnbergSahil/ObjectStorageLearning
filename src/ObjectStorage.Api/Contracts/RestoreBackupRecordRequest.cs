namespace ObjectStorage.Api.Contracts;

public sealed record RestoreBackupRecordRequest(
    string TargetContainerName,
    string? MongoUri,
    string? SourceDatabaseName = null,
    string? TargetDatabaseName = null,
    bool? IsCompressed = null,
    bool DropExisting = true);
