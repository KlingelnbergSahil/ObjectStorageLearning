using System.Text.Json;
using ObjectStorage.Backup.Models;

namespace ObjectStorage.Backup.Services;

public sealed class MongoInspector
{
    private readonly DockerCommandRunner _docker;
    private readonly BackupStrategySelector _strategySelector;

    public MongoInspector(
        DockerCommandRunner docker,
        BackupStrategySelector strategySelector)
    {
        _docker = docker;
        _strategySelector = strategySelector;
    }

    public async Task<IReadOnlyList<MongoContainerInfo>> ListMongoContainersAsync(
        CancellationToken cancellationToken = default)
    {
        BackupCommandResult result =
            await _docker.RunDockerAsync(
                [
                    "ps",
                    "--format",
                    "{{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}"
                ],
                cancellationToken);

        EnsureSuccess(result);

        return result.StandardOutput
            .Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(line => line.Split('\t'))
            .Where(parts =>
                parts.Length >= 4 &&
                (parts[0].Contains("mongo", StringComparison.OrdinalIgnoreCase) ||
                 parts[1].Contains("mongo", StringComparison.OrdinalIgnoreCase) ||
                 parts[1].Contains("percona", StringComparison.OrdinalIgnoreCase)))
            .Select(parts =>
                new MongoContainerInfo(
                    parts[0],
                    parts[1],
                    parts[2],
                    parts[3]))
            .ToList();
    }

    public async Task<IReadOnlyList<MongoDatabaseInfo>> ListDatabasesAsync(
        string containerName,
        CancellationToken cancellationToken = default)
    {
        DockerNameValidator.ValidateContainerName(containerName);

        BackupCommandResult result =
            await _docker.RunDockerAsync(
                [
                    "exec",
                    containerName,
                    "mongosh",
                    "--quiet",
                    "--eval",
                    "JSON.stringify(db.adminCommand({listDatabases:1}).databases)"
                ],
                cancellationToken);

        EnsureSuccess(result);

        using JsonDocument document =
            JsonDocument.Parse(
                result.StandardOutput.Trim());

        return document.RootElement
            .EnumerateArray()
            .Select(item =>
                new MongoDatabaseInfo(
                    item.GetProperty("name").GetString() ?? string.Empty,
                    GetInt64OrZero(item, "sizeOnDisk"),
                    item.TryGetProperty("empty", out JsonElement empty) &&
                    empty.ValueKind == JsonValueKind.True))
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.Name))
            .ToList();
    }

    public async Task<DatabaseSizeInfo> GetDatabaseSizeAsync(
        string containerName,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        DockerNameValidator.ValidateContainerName(containerName);
        DockerNameValidator.ValidateDatabaseName(databaseName);

        string databaseNameLiteral =
            JsonSerializer.Serialize(databaseName);

        BackupCommandResult result =
            await _docker.RunDockerAsync(
                [
                    "exec",
                    containerName,
                    "mongosh",
                    "--quiet",
                    "--eval",
                    $"JSON.stringify(db.getSiblingDB({databaseNameLiteral}).stats())"
                ],
                cancellationToken);

        EnsureSuccess(result);

        using JsonDocument document =
            JsonDocument.Parse(
                result.StandardOutput.Trim());

        long dataSize =
            GetInt64OrZero(
                document.RootElement,
                "dataSize");

        long storageSize =
            GetInt64OrZero(
                document.RootElement,
                "storageSize");

        long indexSize =
            GetInt64OrZero(
                document.RootElement,
                "indexSize");

        return new DatabaseSizeInfo(
            containerName,
            databaseName,
            dataSize,
            storageSize,
            indexSize,
            _strategySelector.Select(storageSize));
    }

    private static long GetInt64OrZero(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out JsonElement value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out long result) =>
                result,
            _ => 0
        };
    }

    private static void EnsureSuccess(
        BackupCommandResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Docker command failed: {result.StandardError}");
        }
    }
}
