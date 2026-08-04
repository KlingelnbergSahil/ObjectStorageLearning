using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ObjectStorage.Backup.Configuration;
using ObjectStorage.Backup.Models;

namespace ObjectStorage.Backup.Services;

public sealed class BackupCatalog
{
    private readonly string _catalogPath;
    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    public BackupCatalog(
        IOptions<BackupOptions> options,
        IHostEnvironment hostEnvironment)
    {
        _catalogPath =
            Path.IsPathRooted(options.Value.CatalogPath)
                ? options.Value.CatalogPath
                : Path.GetFullPath(
                    Path.Combine(
                        hostEnvironment.ContentRootPath,
                        options.Value.CatalogPath));
    }

    public async Task<IReadOnlyList<BackupRecord>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_catalogPath))
        {
            return [];
        }

        await using FileStream stream =
            File.OpenRead(_catalogPath);

        return await JsonSerializer.DeserializeAsync<List<BackupRecord>>(
                stream,
                _jsonOptions,
                cancellationToken)
            ?? [];
    }

    public async Task<BackupRecord?> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BackupRecord> records =
            await ListAsync(cancellationToken);

        return records.FirstOrDefault(
            record => record.Id == id);
    }

    public async Task UpsertAsync(
        BackupRecord record,
        CancellationToken cancellationToken = default)
    {
        List<BackupRecord> records =
            (await ListAsync(cancellationToken)).ToList();

        int index =
            records.FindIndex(
                existing => existing.Id == record.Id);

        if (index >= 0)
        {
            records[index] = record;
        }
        else
        {
            records.Add(record);
        }

        records =
            records
                .OrderByDescending(item => item.CreatedAt)
                .ToList();

        Directory.CreateDirectory(
            Path.GetDirectoryName(_catalogPath)
            ?? throw new InvalidOperationException(
                "Catalog path has no directory."));

        await using FileStream stream =
            File.Create(_catalogPath);

        await JsonSerializer.SerializeAsync(
            stream,
            records,
            _jsonOptions,
            cancellationToken);
    }
}
