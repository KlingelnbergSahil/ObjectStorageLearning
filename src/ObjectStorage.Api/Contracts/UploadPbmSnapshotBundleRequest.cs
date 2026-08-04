namespace ObjectStorage.Api.Contracts;

public sealed class UploadPbmSnapshotBundleRequest
{
    public IFormFile File { get; init; } = null!;

    public string BackupName { get; init; } = string.Empty;
}
