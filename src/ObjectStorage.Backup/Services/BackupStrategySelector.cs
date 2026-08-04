using ObjectStorage.Backup.Configuration;
using ObjectStorage.Backup.Models;
using Microsoft.Extensions.Options;

namespace ObjectStorage.Backup.Services;

public sealed class BackupStrategySelector
{
    private readonly BackupOptions _options;

    public BackupStrategySelector(
        IOptions<BackupOptions> options)
    {
        _options = options.Value;
    }

    public BackupStrategy Select(
        long storageSizeBytes)
    {
        double sizeGb =
            storageSizeBytes / 1024d / 1024d / 1024d;

        if (sizeGb < _options.SmallDatabaseThresholdGb)
        {
            return BackupStrategy.Mongodump;
        }

        if (sizeGb < _options.MediumDatabaseThresholdGb)
        {
            return BackupStrategy.PbmLogical;
        }

        return BackupStrategy.PbmPhysical;
    }
}
