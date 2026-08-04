namespace ObjectStorage.Backup.Models;

public sealed record BackupCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
