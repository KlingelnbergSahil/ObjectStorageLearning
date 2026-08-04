namespace ObjectStorage.Core.Models;

public sealed record ObjectMetadata(
    string Container,
    string Key,
    long Size,
    string? ContentType,
    string? ETag,
    DateTimeOffset? LastModified,
    IReadOnlyDictionary<string, string> CustomMetadata);