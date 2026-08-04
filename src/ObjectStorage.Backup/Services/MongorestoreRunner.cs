using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObjectStorage.Backup.Configuration;
using ObjectStorage.Core.Abstractions;
using ObjectStorage.Core.Models;

namespace ObjectStorage.Backup.Services;

public sealed class MongorestoreRunner
{
    private readonly BackupOptions _options;
    private readonly DockerCommandRunner _docker;
    private readonly IObjectStorageProvider _storage;
    private readonly ILogger<MongorestoreRunner> _logger;

    public MongorestoreRunner(
        IOptions<BackupOptions> options,
        DockerCommandRunner docker,
        IObjectStorageProvider storage,
        ILogger<MongorestoreRunner> logger)
    {
        _options = options.Value;
        _docker = docker;
        _storage = storage;
        _logger = logger;
    }

    public async Task<string> RestoreAsync(
        string targetContainerName,
        string? mongoUri,
        string storageContainer,
        string storageKey,
        string? sourceDatabaseName,
        string? targetDatabaseName,
        bool isCompressed,
        bool dropExisting,
        CancellationToken cancellationToken = default)
    {
        DockerNameValidator.ValidateContainerName(targetContainerName);
        DockerNameValidator.ValidateStorageKey(storageKey);

        string effectiveMongoUri =
            string.IsNullOrWhiteSpace(mongoUri)
                ? _options.Docker.LabMongoUriInsideContainer
                : mongoUri;

        List<string> arguments =
        [
            "exec",
            "-i",
            targetContainerName,
            "mongorestore",
            $"--uri={effectiveMongoUri}",
            "--archive"
        ];

        if (isCompressed)
        {
            arguments.Add("--gzip");
        }

        if (dropExisting)
        {
            arguments.Add("--drop");
        }

        if (!string.IsNullOrWhiteSpace(sourceDatabaseName) ||
            !string.IsNullOrWhiteSpace(targetDatabaseName))
        {
            DockerNameValidator.ValidateDatabaseName(sourceDatabaseName!);
            DockerNameValidator.ValidateDatabaseName(targetDatabaseName!);

            arguments.Add($"--nsFrom={sourceDatabaseName}.*");
            arguments.Add($"--nsTo={targetDatabaseName}.*");
        }

        using var process =
            await _docker.StartDockerProcessAsync(
                arguments,
                redirectStandardInput: true,
                redirectStandardOutput: true,
                cancellationToken);

        Task<string> stdoutTask =
            process.StandardOutput.ReadToEndAsync(cancellationToken);

        Task<string> stderrTask =
            process.StandardError.ReadToEndAsync(cancellationToken);

        _logger.LogInformation(
            "Streaming object {StorageContainer}/{StorageKey} into mongorestore on {TargetContainerName}",
            storageContainer,
            storageKey,
            targetContainerName);

        await _storage.DownloadToAsync(
            StorageObjectId.Create(
                storageContainer,
                storageKey),
            process.StandardInput.BaseStream,
            cancellationToken);

        await process.StandardInput.BaseStream.FlushAsync(cancellationToken);
        process.StandardInput.Close();

        var result =
            await _docker.ReadProcessResultAsync(
                process,
                stderrTask,
                stdoutTask,
                cancellationToken);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"mongorestore failed: {result.StandardError}");
        }

        return string.Join(
            Environment.NewLine,
            [
                result.StandardOutput,
                result.StandardError
            ]);
    }
}
