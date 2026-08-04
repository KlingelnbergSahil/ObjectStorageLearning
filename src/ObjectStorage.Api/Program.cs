using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using ObjectStorage.Core.Abstractions;
using ObjectStorage.AzureBlob.DependencyInjection;
using ObjectStorage.Backup.DependencyInjection;
using ObjectStorage.S3.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit =
        100L * 1024L * 1024L * 1024L;
});

builder.Services.AddBackupManagement(
    builder.Configuration);

string provider =
    builder.Configuration["ObjectStorage:Provider"]
    ?? throw new InvalidOperationException(
        "ObjectStorage:Provider is not configured.");

switch (provider)
{
    case "S3":
        builder.Services.AddS3ObjectStorage(
            builder.Configuration);
        break;

    case "AzureBlob":
        builder.Services.AddAzureBlobObjectStorage(
            builder.Configuration);
        break;

    default:
        throw new InvalidOperationException(
            $"Unsupported object storage provider: {provider}");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "DevelopmentCors",
        policy =>
        {
            policy
                .WithOrigins(
                    "https://localhost:7001",
                    "http://localhost:5001")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

string? pathBase =
    builder.Configuration["PathBase"];

if (!string.IsNullOrWhiteSpace(pathBase) &&
    pathBase != "/")
{
    app.UsePathBase(pathBase.TrimEnd('/'));
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseCors("DevelopmentCors");
}

app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

QueueDefaultContainerEnsure(app);

app.Run();

static void QueueDefaultContainerEnsure(
    WebApplication application)
{
    application.Lifetime.ApplicationStarted.Register(
        () =>
        {
            _ = Task.Run(
                async () =>
                    await EnsureDefaultContainerAsync(
                        application.Services,
                        application.Configuration,
                        application.Lifetime.ApplicationStopping));
        });
}

static async Task EnsureDefaultContainerAsync(
    IServiceProvider services,
    IConfiguration configuration,
    CancellationToken cancellationToken)
{
    using IServiceScope scope =
        services.CreateScope();

    ILogger logger =
        scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");

    var storage =
        scope.ServiceProvider
            .GetRequiredService<IObjectStorageProvider>();

    string defaultContainer =
        configuration[
            "ObjectStorage:DefaultContainer"]
        ?? throw new InvalidOperationException(
            "ObjectStorage:DefaultContainer is missing.");

    for (int attempt = 1; attempt <= 120; attempt++)
    {
        try
        {
            await storage.EnsureContainerExistsAsync(
                defaultContainer,
                cancellationToken);

            logger.LogInformation(
                "Default storage container {Container} is ready.",
                defaultContainer);
            return;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            if (attempt == 120)
            {
                logger.LogError(
                    exception,
                    "Could not ensure default storage container {Container} after {Attempts} attempts.",
                    defaultContainer,
                    attempt);
                return;
            }

            logger.LogWarning(
                exception,
                "Could not ensure default storage container {Container} on attempt {Attempt}. Retrying.",
                defaultContainer,
                attempt);

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
