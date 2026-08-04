using ObjectStorage.Core.Models;

namespace ObjectStorage.Core.Abstractions;

public interface IObjectStorageProvider
{
    Task EnsureContainerExistsAsync(
        string container,
        CancellationToken cancellationToken = default);

    Task UploadAsync(
        StorageObjectId objectId,
        Stream content,
        string contentType,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task DownloadToAsync(
        StorageObjectId objectId,
        Stream destination,
        CancellationToken cancellationToken = default);

    Task<ObjectMetadata?> GetMetadataAsync(
        StorageObjectId objectId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        StorageObjectId objectId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        StorageObjectId objectId,
        CancellationToken cancellationToken = default);

    Task<TemporaryAccessUrl> CreateUploadUrlAsync(
        StorageObjectId objectId,
        TimeSpan validity,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<TemporaryAccessUrl> CreateDownloadUrlAsync(
        StorageObjectId objectId,
        TimeSpan validity,
        string? downloadFileName = null,
        CancellationToken cancellationToken = default);
}