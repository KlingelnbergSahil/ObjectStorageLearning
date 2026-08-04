using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObjectStorage.Backup.Configuration;
using ObjectStorage.Backup.Models;

namespace ObjectStorage.Backup.Services;

public sealed class DockerCommandRunner
{
    private readonly BackupOptions _options;
    private readonly ILogger<DockerCommandRunner> _logger;

    public DockerCommandRunner(
        IOptions<BackupOptions> options,
        ILogger<DockerCommandRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BackupCommandResult> RunDockerAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync(
            "docker",
            arguments,
            cancellationToken);
    }

    public async Task<Process> StartDockerProcessAsync(
        IReadOnlyList<string> arguments,
        bool redirectStandardInput,
        bool redirectStandardOutput,
        CancellationToken cancellationToken = default)
    {
        var process =
            CreateProcess(
                "docker",
                arguments,
                redirectStandardInput,
                redirectStandardOutput);

        _logger.LogInformation(
            "Starting docker {Arguments}",
            string.Join(' ', arguments));

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Failed to start docker process.");
        }

        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();

        return process;
    }

    public async Task<BackupCommandResult> ReadProcessResultAsync(
        Process process,
        Task<string> standardErrorTask,
        Task<string>? standardOutputTask,
        CancellationToken cancellationToken = default)
    {
        using var timeout =
            new CancellationTokenSource(
                TimeSpan.FromMinutes(
                    _options.CommandTimeoutMinutes));

        using var linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);

        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        string stdout =
            standardOutputTask is null
                ? string.Empty
                : await standardOutputTask;

        string stderr =
            await standardErrorTask;

        return new BackupCommandResult(
            process.ExitCode,
            stdout,
            stderr);
    }

    private async Task<BackupCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using Process process =
            CreateProcess(
                fileName,
                arguments,
                redirectStandardInput: false,
                redirectStandardOutput: true);

        _logger.LogInformation(
            "Running {FileName} {Arguments}",
            fileName,
            string.Join(' ', arguments));

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Failed to start {fileName}.");
        }

        Task<string> stdoutTask =
            process.StandardOutput.ReadToEndAsync(cancellationToken);

        Task<string> stderrTask =
            process.StandardError.ReadToEndAsync(cancellationToken);

        BackupCommandResult result =
            await ReadProcessResultAsync(
                process,
                stderrTask,
                stdoutTask,
                cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "{FileName} failed with exit code {ExitCode}. Stderr: {Stderr}",
                fileName,
                result.ExitCode,
                result.StandardError);
        }

        return result;
    }

    private static Process CreateProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        bool redirectStandardInput,
        bool redirectStandardOutput)
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardInput = redirectStandardInput,
                RedirectStandardOutput = redirectStandardOutput,
                RedirectStandardError = true,
                UseShellExecute = false
            };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
    }

    private static void TryKill(
        Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup after timeout or cancellation.
        }
    }
}
