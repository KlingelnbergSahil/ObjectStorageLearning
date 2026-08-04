using Microsoft.Extensions.Options;
using ObjectStorage.Backup.Configuration;
using ObjectStorage.Backup.Models;
using ObjectStorage.Core.Abstractions;

namespace ObjectStorage.Backup.Services;

public sealed class PbmRunner
{
    private readonly BackupOptions _options;
    private readonly DockerCommandRunner _docker;
    private readonly IObjectStorageProvider _storage;

    public PbmRunner(
        IOptions<BackupOptions> options,
        DockerCommandRunner docker,
        IObjectStorageProvider storage)
    {
        _options = options.Value;
        _docker = docker;
        _storage = storage;
    }

    public async Task<BackupCommandResult> ConfigureAsync(
        CancellationToken cancellationToken = default)
    {
        await _storage.EnsureContainerExistsAsync(
            _options.PbmBackupContainer,
            cancellationToken);

        return await RunPbmAsync(
            [
                "config",
                "--file",
                _options.Docker.PbmStorageConfigPath
            ],
            cancellationToken);
    }

    public async Task<BackupCommandResult> StatusAsync(
        CancellationToken cancellationToken = default)
    {
        return await RunPbmAsync(
            ["status"],
            cancellationToken);
    }

    public async Task<BackupCommandResult> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return await RunPbmAsync(
            ["list"],
            cancellationToken);
    }

    public async Task<BackupCommandResult> LogsAsync(
        int tail,
        CancellationToken cancellationToken = default)
    {
        int boundedTail =
            Math.Clamp(
                tail,
                20,
                500);

        return await _docker.RunDockerAsync(
            [
                "logs",
                "--tail",
                boundedTail.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                _options.Docker.PbmAgentContainerName
            ],
            cancellationToken);
    }

    public async Task<BackupCommandResult> BackupAsync(
        BackupStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        string type =
            strategy switch
            {
                BackupStrategy.PbmLogical => "logical",
                BackupStrategy.PbmPhysical => "physical",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(strategy),
                    "PBM only supports logical or physical backup strategies.")
            };

        return await RunPbmAsync(
            [
                "backup",
                $"--type={type}"
            ],
            cancellationToken);
    }

    public async Task<BackupCommandResult> RestoreAsync(
        string backupName,
        CancellationToken cancellationToken = default)
    {
        DockerNameValidator.ValidatePbmBackupName(backupName);

        return await RunPbmAsync(
            [
                "restore",
                backupName,
                "--yes"
            ],
            cancellationToken);
    }

    public async Task<BackupCommandResult> DropDatabaseAsync(
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        DockerNameValidator.ValidateDatabaseName(databaseName);

        return await _docker.RunDockerAsync(
            [
                "exec",
                _options.Docker.LabMongoContainerName,
                "mongosh",
                "--quiet",
                "--eval",
                $"db.getSiblingDB(\"{databaseName}\").dropDatabase()"
            ],
            cancellationToken);
    }

    public async Task<BackupCommandResult> ResyncAsync(
        CancellationToken cancellationToken = default)
    {
        return await RunPbmAsync(
            [
                "config",
                "--force-resync"
            ],
            cancellationToken);
    }

    private async Task<BackupCommandResult> RunPbmAsync(
        IReadOnlyList<string> pbmArguments,
        CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "exec",
            _options.Docker.PbmAgentContainerName,
            "pbm"
        ];

        arguments.AddRange(pbmArguments);

        return await _docker.RunDockerAsync(
            arguments,
            cancellationToken);
    }
}
