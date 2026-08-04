using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObjectStorage.AzureBlob.Configuration;
using ObjectStorage.Core.Abstractions;

namespace ObjectStorage.AzureBlob.DependencyInjection;

public static class AzureBlobServiceCollectionExtensions
{
    public static IServiceCollection AddAzureBlobObjectStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AzureBlobStorageOptions>()
            .Bind(
                configuration.GetSection(
                    AzureBlobStorageOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ConnectionString) ||
                    Uri.TryCreate(
                        options.ServiceUri,
                        UriKind.Absolute,
                        out _),
                "Azure Blob requires a connection string or a valid service URI.")
            .ValidateOnStart();

        services.AddSingleton<IObjectStorageProvider>(
            _ => throw new NotSupportedException(
                "Azure Blob object storage is planned for Milestone 3."));

        return services;
    }
}
