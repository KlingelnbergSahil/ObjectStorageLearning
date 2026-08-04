namespace ObjectStorage.Api.Contracts;

public sealed class ServerUploadRequest
{
    public IFormFile File { get; init; } = null!;

    public string Container { get; init; } = string.Empty;

    public string ObjectKey { get; init; } = string.Empty;
}
