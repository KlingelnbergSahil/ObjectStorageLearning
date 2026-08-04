namespace ObjectStorage.AzureBlob.Configuration;

public sealed class AzureBlobStorageOptions
{
    public const string SectionName = "ObjectStorage:AzureBlob";

    public string ServiceUri { get; set; } = string.Empty;

    public string ConnectionString { get; set; } = string.Empty;

    public string DefaultContainer { get; set; } = "ge-files";

    public int SasExpiryMinutes { get; set; } = 15;
}
