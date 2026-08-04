namespace ObjectStorage.Backup.Configuration;

public sealed class DockerOptions
{
    public string SourceMongoContainerName { get; set; } = "backup-poc-mongo";

    public string LabMongoContainerName { get; set; } = "backup-poc-mongo";

    public string PbmAgentContainerName { get; set; } = "backup-poc-pbm-agent";

    public string LabMongoUriInsideContainer { get; set; } =
        "mongodb://localhost:27017/?replicaSet=rs0";

    public string SourceMongoUriInsideContainer { get; set; } =
        "mongodb://localhost:27017/?replicaSet=rs0";

    public string PbmStorageConfigPath { get; set; } = "/etc/pbm/pbm-storage.yaml";
}
