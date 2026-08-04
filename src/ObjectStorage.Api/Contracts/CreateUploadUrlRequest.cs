namespace ObjectStorage.Api.Contracts;

public sealed record CreateUploadUrlRequest(
    string Container,
    string ObjectKey,
    string ContentType,
    int ExpiryMinutes = 15);