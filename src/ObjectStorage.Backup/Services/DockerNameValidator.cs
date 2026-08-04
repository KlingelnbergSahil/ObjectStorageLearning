using System.Text.RegularExpressions;

namespace ObjectStorage.Backup.Services;

internal static partial class DockerNameValidator
{
    public static void ValidateContainerName(
        string value)
    {
        ValidateToken(
            value,
            nameof(value));
    }

    public static void ValidateDatabaseName(
        string value)
    {
        ValidateToken(
            value,
            nameof(value));
    }

    public static void ValidateStorageKey(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Contains("..", StringComparison.Ordinal) ||
            value.StartsWith('/'))
        {
            throw new ArgumentException(
                "Storage keys must be relative object keys.");
        }
    }

    public static void ValidatePbmBackupName(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "PBM backup names cannot contain control characters.",
                nameof(value));
        }
    }

    private static void ValidateToken(
        string value,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!SafeTokenRegex().IsMatch(value))
        {
            throw new ArgumentException(
                "Only letters, digits, dot, underscore, and hyphen are allowed.",
                parameterName);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex SafeTokenRegex();
}
