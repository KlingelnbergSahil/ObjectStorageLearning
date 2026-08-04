using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObjectStorage.Backup.Configuration;
using ObjectStorage.Backup.Models;
using ObjectStorage.Core.Abstractions;
using ObjectStorage.Core.Models;

namespace ObjectStorage.Backup.Services;

public sealed class MongodumpBackupRunner
{
    private readonly BackupOptions _options;
    private readonly DockerCommandRunner _docker;
    private readonly IObjectStorageProvider _storage;
    private readonly BackupCatalog _catalog;
    private readonly ILogger<MongodumpBackupRunner> _logger;

    public MongodumpBackupRunner(
        IOptions<BackupOptions> options,
        DockerCommandRunner docker,
        IObjectStorageProvider storage,
        BackupCatalog catalog,
        ILogger<MongodumpBackupRunner> logger)
    {
        _options = options.Value;
        _docker = docker;
        _storage = storage;
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<BackupRecord> BackupAsync(
        string sourceContainerName,
        string databaseName,
        string? mongoUri,
        string? storageContainer,
        string? storageKey,
        CancellationToken cancellationToken = default)
    {
        DockerNameValidator.ValidateContainerName(sourceContainerName);
        DockerNameValidator.ValidateDatabaseName(databaseName);

        string container =
            string.IsNullOrWhiteSpace(storageContainer)
                ? _options.DefaultBackupContainer
                : storageContainer;

        string id =
            BackupName.CreateId(
                databaseName,
                "mongodump");

        string key =
            string.IsNullOrWhiteSpace(storageKey)
                ? $"mongodump/{databaseName}/{id}.archive.gz"
                : storageKey;

        DockerNameValidator.ValidateStorageKey(key);

        var runningRecord =
            new BackupRecord(
                id,
                databaseName,
                BackupStrategy.Mongodump,
                BackupStatus.Running,
                container,
                key,
                DateTimeOffset.UtcNow,
                null,
                null,
                sourceContainerName,
                null,
                null,
                null);

        await _catalog.UpsertAsync(
            runningRecord,
            cancellationToken);

        await _storage.EnsureContainerExistsAsync(
            container,
            cancellationToken);

        string effectiveMongoUri =
            string.IsNullOrWhiteSpace(mongoUri)
                ? _options.Docker.SourceMongoUriInsideContainer
                : mongoUri;

        using var process =
            await _docker.StartDockerProcessAsync(
                [
                    "exec",
                    sourceContainerName,
                    "mongodump",
                    $"--uri={effectiveMongoUri}",
                    $"--db={databaseName}",
                    "--archive",
                    "--gzip"
                ],
                redirectStandardInput: false,
                redirectStandardOutput: true,
                cancellationToken);

        Task<string> stderrTask =
            process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            _logger.LogInformation(
                "Streaming mongodump for {DatabaseName} from {SourceContainerName} to {StorageContainer}/{StorageKey}",
                databaseName,
                sourceContainerName,
                container,
                key);

            await _storage.UploadAsync(
                StorageObjectId.Create(
                    container,
                    key),
                process.StandardOutput.BaseStream,
                "application/gzip",
                new Dictionary<string, string>
                {
                    ["backup-tool"] = "mongodump",
                    ["database"] = databaseName,
                    ["source-container"] = sourceContainerName
                },
                cancellationToken);

            BackupCommandResult result =
                await _docker.ReadProcessResultAsync(
                    process,
                    stderrTask,
                    standardOutputTask: null,
                    cancellationToken);

            BackupRecord completed =
                runningRecord with
                {
                    Status = result.Succeeded
                        ? BackupStatus.Completed
                        : BackupStatus.Failed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ToolOutput = result.StandardError,
                    Error = result.Succeeded
                        ? null
                        : result.StandardError
                };

            await _catalog.UpsertAsync(
                completed,
                cancellationToken);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"mongodump failed: {result.StandardError}");
            }

            return completed;
        }
        catch (Exception exception)
        {
            BackupRecord failed =
                runningRecord with
                {
                    Status = BackupStatus.Failed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Error = exception.Message
                };

            await _catalog.UpsertAsync(
                failed,
                cancellationToken);

            throw;
        }
    }
}
