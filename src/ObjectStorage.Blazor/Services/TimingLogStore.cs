using System.Text.Json;
using System.Text.Json.Serialization;
using ObjectStorage.Blazor.Models;

namespace ObjectStorage.Blazor.Services;

public sealed class TimingLogStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TimingLogStore(
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        string configuredPath =
            configuration["TimingLog:Path"]
            ?? "/data/timings/timing-log.json";

        _path =
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(
                    Path.Combine(
                        environment.ContentRootPath,
                        configuredPath));
    }

    public async Task<IReadOnlyList<TimingEntry>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadUnsafeAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(
        TimingEntry entry,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            List<TimingEntry> entries =
                (await ReadUnsafeAsync(cancellationToken)).ToList();

            entries.Insert(
                0,
                entry);

            Directory.CreateDirectory(
                Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException(
                    "Timing log path has no directory."));

            await using FileStream stream =
                File.Create(_path);

            await JsonSerializer.SerializeAsync(
                stream,
                entries,
                _jsonOptions,
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException(
                    "Timing log path has no directory."));

            await using FileStream stream =
                File.Create(_path);

            await JsonSerializer.SerializeAsync(
                stream,
                Array.Empty<TimingEntry>(),
                _jsonOptions,
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<TimingEntry>> ReadUnsafeAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        await using FileStream stream =
            File.OpenRead(_path);

        return await JsonSerializer.DeserializeAsync<List<TimingEntry>>(
                stream,
                _jsonOptions,
                cancellationToken)
            ?? [];
    }
}
