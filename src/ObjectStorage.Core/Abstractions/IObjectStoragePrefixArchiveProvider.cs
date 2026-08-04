namespace ObjectStorage.Core.Abstractions;

public interface IObjectStoragePrefixArchiveProvider
{
    Task StreamPrefixAsZipAsync(
        string container,
        string prefix,
        Stream destination,
        CancellationToken cancellationToken = default);
}
