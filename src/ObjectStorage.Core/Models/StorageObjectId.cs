namespace ObjectStorage.Core.Models
{
    public sealed record StorageObjectId(
        string Container,
        string Key)
    {
        public static StorageObjectId Create(
            string container,
            string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(container);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            string normalizedKey = key
                .Replace('\\', '/')
                .TrimStart('/');

            return new StorageObjectId(
                container.Trim(),
                normalizedKey);
        }
    }
}
