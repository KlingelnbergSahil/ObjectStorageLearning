namespace ObjectStorage.Core.Models;

public sealed record TemporaryAccessUrl(
    Uri Url,
    string HttpMethod,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);