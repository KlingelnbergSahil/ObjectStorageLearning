namespace ObjectStorage.Api.Contracts;

public sealed record CreateDownloadUrlRequest(
    string Container,
    string ObjectKey,
    string? DownloadFileName,
    int ExpiryMinutes = 15);