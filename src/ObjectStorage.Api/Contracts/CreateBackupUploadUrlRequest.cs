namespace ObjectStorage.Api.Contracts;

public sealed record CreateBackupUploadUrlRequest(
    string StorageContainer,
    string StorageKey,
    string ContentType = "application/gzip",
    int ExpiryMinutes = 15);
