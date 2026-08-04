using System.IO.Compression;
using Amazon.S3;
using Amazon.S3.Model;
using ObjectStorage.Core.Abstractions;

namespace ObjectStorage.S3.Services;

public sealed class S3PrefixArchiveProvider : IObjectStoragePrefixArchiveProvider
{
    private readonly IAmazonS3 _s3Client;

    public S3PrefixArchiveProvider(
        IAmazonS3 s3Client)
    {
        _s3Client = s3Client;
    }

    public async Task StreamPrefixAsZipAsync(
        string container,
        string prefix,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(container);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(destination);

        string normalizedPrefix =
            prefix.TrimStart('/').TrimEnd('/') + "/";

        using var archiveDestination =
            new AsyncWriteOnlyStream(destination, cancellationToken);

        using var archive =
            new ZipArchive(
                archiveDestination,
                ZipArchiveMode.Create,
                leaveOpen: true);

        string? continuationToken = null;

        do
        {
            ListObjectsV2Response listResponse =
                await _s3Client.ListObjectsV2Async(
                    new ListObjectsV2Request
                    {
                        BucketName = container,
                        Prefix = normalizedPrefix,
                        ContinuationToken = continuationToken
                    },
                    cancellationToken);

            foreach (S3Object item in listResponse.S3Objects)
            {
                string entryName =
                    item.Key[normalizedPrefix.Length..];

                if (string.IsNullOrWhiteSpace(entryName))
                {
                    continue;
                }

                ZipArchiveEntry entry =
                    archive.CreateEntry(
                        entryName,
                        CompressionLevel.NoCompression);

                await using Stream entryStream =
                    entry.Open();

                using GetObjectResponse objectResponse =
                    await _s3Client.GetObjectAsync(
                        new GetObjectRequest
                        {
                            BucketName = container,
                            Key = item.Key
                        },
                        cancellationToken);

                await objectResponse.ResponseStream.CopyToAsync(
                    entryStream,
                    cancellationToken);
            }

            continuationToken =
                listResponse.IsTruncated == true
                    ? listResponse.NextContinuationToken
                    : null;
        }
        while (!string.IsNullOrWhiteSpace(continuationToken));
    }

    private sealed class AsyncWriteOnlyStream : Stream
    {
        private readonly Stream _inner;
        private readonly CancellationToken _cancellationToken;

        public AsyncWriteOnlyStream(Stream inner, CancellationToken cancellationToken)
        {
            _inner = inner;
            _cancellationToken = cancellationToken;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() =>
            _inner.FlushAsync(_cancellationToken).GetAwaiter().GetResult();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            _inner.WriteAsync(buffer.AsMemory(offset, count), _cancellationToken)
                .AsTask()
                .GetAwaiter()
                .GetResult();

        public override void Write(ReadOnlySpan<byte> buffer) =>
            _inner.WriteAsync(buffer.ToArray(), _cancellationToken)
                .AsTask()
                .GetAwaiter()
                .GetResult();

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.WriteAsync(buffer, cancellationToken);
    }
}
