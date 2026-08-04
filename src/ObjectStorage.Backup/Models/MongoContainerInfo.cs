namespace ObjectStorage.Backup.Models;

public sealed record MongoContainerInfo(
    string Name,
    string Image,
    string Status,
    string Ports);
