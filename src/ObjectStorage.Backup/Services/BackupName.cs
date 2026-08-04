namespace ObjectStorage.Backup.Services;

internal static class BackupName
{
    public static string CreateId(
        string databaseName,
        string suffix)
    {
        string safeDatabaseName =
            SafeToken(databaseName);

        return $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{safeDatabaseName}-{suffix}";
    }

    public static string SafeToken(
        string value)
    {
        return string.Concat(
            value.Select(
                character =>
                    char.IsLetterOrDigit(character) ||
                    character is '-' or '_' or '.'
                        ? character
                        : '-'))
            .Trim('-');
    }
}
