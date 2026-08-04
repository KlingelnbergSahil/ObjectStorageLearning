namespace ObjectStorage.Backup.Models;

public sealed record DatabaseSizeInfo(
    string ContainerName,
    string DatabaseName,
    long DataSizeBytes,
    long StorageSizeBytes,
    long IndexSizeBytes,
    BackupStrategy SelectedStrategy);
