using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObjectStorage.Backup.Configuration;
using ObjectStorage.Backup.Services;

namespace ObjectStorage.Backup.DependencyInjection;

public static class BackupServiceCollectionExtensions
{
    public static IServiceCollection AddBackupManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<BackupOptions>()
            .Bind(
                configuration.GetSection(
                    BackupOptions.SectionName))
            .Validate(
                options => options.SmallDatabaseThresholdGb > 0,
                "Backup small database threshold must be greater than zero.")
            .Validate(
                options =>
                    options.MediumDatabaseThresholdGb >
                    options.SmallDatabaseThresholdGb,
                "Backup medium database threshold must be greater than the small threshold.")
            .ValidateOnStart();

        services.AddSingleton<BackupStrategySelector>();
        services.AddSingleton<DockerCommandRunner>();
        services.AddSingleton<BackupCatalog>();
        services.AddSingleton<MongoInspector>();
        services.AddSingleton<MongodumpBackupRunner>();
        services.AddSingleton<MongorestoreRunner>();
        services.AddSingleton<PbmRunner>();

        return services;
    }
}
