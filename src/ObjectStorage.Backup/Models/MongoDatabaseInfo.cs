namespace ObjectStorage.Backup.Models;

public sealed record MongoDatabaseInfo(
    string Name,
    long SizeOnDiskBytes,
    bool Empty);
