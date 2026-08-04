namespace ObjectStorage.Api.Contracts;

public sealed record CreateBackupDownloadUrlRequest(
    string StorageContainer,
    string StorageKey,
    string? DownloadFileName,
    int ExpiryMinutes = 15);
