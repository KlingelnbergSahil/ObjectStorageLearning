namespace ObjectStorage.Backup.Configuration;

public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    public string DefaultBackupContainer { get; set; } = "ge-backups";

    public string PbmBackupContainer { get; set; } = "ge-pbm-backups";

    public double SmallDatabaseThresholdGb { get; set; } = 20;

    public double MediumDatabaseThresholdGb { get; set; } = 100;

    public int CommandTimeoutMinutes { get; set; } = 120;

    public string CatalogPath { get; set; } = "../../../data/backups/catalog.json";

    public DockerOptions Docker { get; set; } = new();
}
