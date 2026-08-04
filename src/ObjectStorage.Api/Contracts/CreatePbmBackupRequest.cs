using ObjectStorage.Backup.Models;

namespace ObjectStorage.Api.Contracts;

public sealed record CreatePbmBackupRequest(
    BackupStrategy Strategy);
