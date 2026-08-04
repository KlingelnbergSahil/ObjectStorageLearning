namespace ObjectStorage.Api.Contracts;

public sealed record CreateMongorestoreRequest(
    string TargetContainerName,
    string? MongoUri,
    string StorageContainer,
    string StorageKey,
    string? SourceDatabaseName = null,
    string? TargetDatabaseName = null,
    bool IsCompressed = true,
    bool DropExisting = true);
