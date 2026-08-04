using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ObjectStorage.Blazor.Models;

namespace ObjectStorage.Blazor.Services;

public sealed class BackupApiClient
{
    private readonly HttpClient _httpClient;
    private readonly Uri _publicBaseAddress;
    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

    public BackupApiClient(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _publicBaseAddress =
            new Uri(
                configuration["BackupApi:PublicBaseUrl"]
                ?? httpClient.BaseAddress?.ToString()
                ?? "http://localhost:5213");
    }

    public async Task<IReadOnlyList<BackupRecord>> GetRecordsAsync()
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<BackupRecord>>(
                "api/backup/records",
                _jsonOptions)
            ?? [];
    }

    public async Task<IReadOnlyList<MongoContainerInfo>> GetMongoContainersAsync()
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<MongoContainerInfo>>(
                "api/backup/mongo-containers",
                _jsonOptions)
            ?? [];
    }

    public async Task<IReadOnlyList<MongoDatabaseInfo>> GetDatabasesAsync(
        string containerName)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<MongoDatabaseInfo>>(
                $"api/backup/mongo-containers/{Uri.EscapeDataString(containerName)}/databases",
                _jsonOptions)
            ?? [];
    }

    public async Task<DatabaseSizeInfo?> GetDatabaseSizeAsync(
        string containerName,
        string databaseName)
    {
        return await _httpClient.GetFromJsonAsync<DatabaseSizeInfo>(
            $"api/backup/mongo-containers/{Uri.EscapeDataString(containerName)}/databases/{Uri.EscapeDataString(databaseName)}/size",
            _jsonOptions);
    }

    public async Task<BackupRecord?> CreateMongodumpBackupAsync(
        CreateMongodumpBackupRequest request)
    {
        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                "api/backup/mongodump",
                request,
                _jsonOptions);

        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<BackupRecord>(
            _jsonOptions);
    }

    public async Task<TemporaryAccessUrl?> CreateUploadUrlAsync(
        CreateBackupUploadUrlRequest request)
    {
        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                "api/backup/upload-url",
                request,
                _jsonOptions);

        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<TemporaryAccessUrl>(
            _jsonOptions);
    }

    public async Task<TemporaryAccessUrl?> CreateDownloadUrlAsync(
        CreateBackupDownloadUrlRequest request)
    {
        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                "api/backup/download-url",
                request,
                _jsonOptions);

        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<TemporaryAccessUrl>(
            _jsonOptions);
    }

    public async Task<BackupRecord?> RegisterUploadedBackupAsync(
        RegisterUploadedBackupRequest request)
    {
        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                "api/backup/records/uploaded",
                request,
                _jsonOptions);

        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<BackupRecord>(
            _jsonOptions);
    }

    public async Task<string> RestoreRecordAsync(
        string recordId,
        RestoreBackupRecordRequest request)
    {
        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                $"api/backup/records/{Uri.EscapeDataString(recordId)}/restore",
                request,
                _jsonOptions);

        return await ReadCommandBodyAsync(response);
    }

    public async Task<CommandResultResponse?> ConfigurePbmAsync()
    {
        using HttpResponseMessage response =
            await _httpClient.PostAsync(
                "api/backup/pbm/configure",
                null);

        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<CommandResultResponse>(
            _jsonOptions);
    }

    public async Task<CommandResultResponse?> GetPbmStatusAsync()
    {
        return await _httpClient.GetFromJsonAsync<CommandResultResponse>(
            "api/backup/pbm/status",
            _jsonOptions);
    }

    public async Task<CommandResultResponse?> GetPbmListAsync()
    {
        return await _httpClient.GetFromJsonAsync<CommandResultResponse>(
            "api/backup/pbm/list",
            _jsonOptions);
    }

    public async Task<CommandResultResponse?> GetPbmLogsAsync(
        int tail = 120)
    {
        return await _httpClient.GetFromJsonAsync<CommandResultResponse>(
            $"api/backup/pbm/logs?tail={tail}",
            _jsonOptions);
    }

    public async Task<CommandResultResponse?> CreatePbmBackupAsync(
        BackupStrategy strategy)
    {
        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                "api/backup/pbm/backup",
                new CreatePbmBackupRequest(strategy),
                _jsonOptions);

        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<CommandResultResponse>(
            _jsonOptions);
    }

    public async Task<CommandResultResponse?> RestorePbmAsync(
        string backupName,
        bool dropExistingData = false,
        string? databaseNameToDrop = null)
    {
        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                "api/backup/pbm/restore",
                new CreatePbmRestoreRequest(
                    backupName,
                    dropExistingData,
                    databaseNameToDrop),
                _jsonOptions);

        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<CommandResultResponse>(
            _jsonOptions);
    }

    public Uri CreatePbmSnapshotDownloadUri(
        string backupName)
    {
        return new Uri(
            _publicBaseAddress,
            $"api/backup/pbm/snapshots/{Uri.EscapeDataString(backupName)}/download");
    }

    private static async Task<string> ReadCommandBodyAsync(
        HttpResponseMessage response)
    {
        string body =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                body);
        }

        return body;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body =
            await response.Content.ReadAsStringAsync();

        throw new InvalidOperationException(
            body);
    }
}
