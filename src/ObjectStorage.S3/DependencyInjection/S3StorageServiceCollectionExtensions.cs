using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ObjectStorage.Core.Abstractions;
using ObjectStorage.S3.Configuration;
using ObjectStorage.S3.Services;

namespace ObjectStorage.S3.DependencyInjection;

public static class S3StorageServiceCollectionExtensions
{
    public static IServiceCollection AddS3ObjectStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<S3StorageOptions>()
            .Bind(
                configuration.GetSection(
                    S3StorageOptions.SectionName))
            .Validate(
                options =>
                    Uri.TryCreate(
                        options.ServiceUrl,
                        UriKind.Absolute,
                        out _),
                "A valid S3 ServiceUrl is required.")
            .Validate(
                options =>
                    string.IsNullOrWhiteSpace(options.PublicServiceUrl) ||
                    Uri.TryCreate(
                        options.PublicServiceUrl,
                        UriKind.Absolute,
                        out _),
                "S3 PublicServiceUrl must be empty or a valid absolute URL.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.AccessKey),
                "S3 AccessKey is required.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.SecretKey),
                "S3 SecretKey is required.")
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            S3StorageOptions options =
                serviceProvider
                    .GetRequiredService<IOptions<S3StorageOptions>>()
                    .Value;

            var credentials =
                new BasicAWSCredentials(
                    options.AccessKey,
                    options.SecretKey);

            var clientConfiguration =
                new AmazonS3Config
                {
                    ServiceURL = options.ServiceUrl,
                    ForcePathStyle = options.ForcePathStyle,
                    AuthenticationRegion = options.Region,
                    UseHttp = options.UseHttp
                };

            return new AmazonS3Client(
                credentials,
                clientConfiguration);
        });

        services.AddSingleton<
            IObjectStorageProvider,
            S3ObjectStorageProvider>();

        services.AddSingleton<
            IObjectStoragePrefixArchiveProvider,
            S3PrefixArchiveProvider>();

        return services;
    }
}
