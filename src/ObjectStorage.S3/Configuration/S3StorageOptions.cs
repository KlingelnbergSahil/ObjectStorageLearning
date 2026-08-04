namespace ObjectStorage.S3.Configuration;

public sealed class S3StorageOptions
{
    public const string SectionName = "ObjectStorage:S3";

    public string ServiceUrl { get; set; } = string.Empty;

    public string PublicServiceUrl { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public bool ForcePathStyle { get; set; } = true;

    public bool UseHttp { get; set; }

    public int PresignedUrlExpiryMinutes { get; set; } = 15;
}