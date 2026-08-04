using Microsoft.AspNetCore.Mvc;
using ObjectStorage.Api.Contracts;
using ObjectStorage.Core.Abstractions;
using ObjectStorage.Core.Models;

namespace ObjectStorage.Api.Controllers;

[ApiController]
[Route("api/storage")]
public sealed class StorageController : ControllerBase
{
    private readonly IObjectStorageProvider _storage;

    public StorageController(
        IObjectStorageProvider storage)
    {
        _storage = storage;
    }

    [HttpPost("containers/{container}")]
    public async Task<IActionResult> EnsureContainerAsync(
        string container,
        CancellationToken cancellationToken)
    {
        await _storage.EnsureContainerExistsAsync(
            container,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("server-upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1024L * 1024L * 1024L)]
    public async Task<IActionResult> ServerUploadAsync(
        [FromForm] ServerUploadRequest request,
        CancellationToken cancellationToken)
    {
        IFormFile file = request.File;

        if (file.Length == 0)
        {
            return BadRequest("The uploaded file is empty.");
        }

        StorageObjectId objectId =
            StorageObjectId.Create(
                request.Container,
                request.ObjectKey);

        await using Stream input =
            file.OpenReadStream();

        await _storage.UploadAsync(
            objectId,
            input,
            file.ContentType ?? "application/octet-stream",
            new Dictionary<string, string>
            {
                ["original-file-name"] =
                    Path.GetFileName(file.FileName)
            },
            cancellationToken);

        return Created(
            $"/api/storage/metadata?container={Uri.EscapeDataString(request.Container)}&objectKey={Uri.EscapeDataString(request.ObjectKey)}",
            new
            {
                objectId.Container,
                objectId.Key,
                file.Length
            });
    }

    [HttpPost("upload-url")]
    public async Task<ActionResult<TemporaryAccessUrl>>
        CreateUploadUrlAsync(
            [FromBody] CreateUploadUrlRequest request,
            CancellationToken cancellationToken)
    {
        StorageObjectId objectId =
            StorageObjectId.Create(
                request.Container,
                request.ObjectKey);

        TimeSpan validity =
            ValidateExpiry(request.ExpiryMinutes);

        TemporaryAccessUrl result =
            await _storage.CreateUploadUrlAsync(
                objectId,
                validity,
                request.ContentType,
                cancellationToken);

        return Ok(result);
    }

    [HttpPost("download-url")]
    public async Task<ActionResult<TemporaryAccessUrl>>
        CreateDownloadUrlAsync(
            [FromBody] CreateDownloadUrlRequest request,
            CancellationToken cancellationToken)
    {
        StorageObjectId objectId =
            StorageObjectId.Create(
                request.Container,
                request.ObjectKey);

        bool exists =
            await _storage.ExistsAsync(
                objectId,
                cancellationToken);

        if (!exists)
        {
            return NotFound();
        }

        TemporaryAccessUrl result =
            await _storage.CreateDownloadUrlAsync(
                objectId,
                ValidateExpiry(request.ExpiryMinutes),
                request.DownloadFileName,
                cancellationToken);

        return Ok(result);
    }

    [HttpGet("metadata")]
    public async Task<ActionResult<ObjectMetadata>>
        GetMetadataAsync(
            [FromQuery] string container,
            [FromQuery] string objectKey,
            CancellationToken cancellationToken)
    {
        StorageObjectId objectId =
            StorageObjectId.Create(
                container,
                objectKey);

        ObjectMetadata? metadata =
            await _storage.GetMetadataAsync(
                objectId,
                cancellationToken);

        return metadata is null
            ? NotFound()
            : Ok(metadata);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAsync(
        [FromQuery] string container,
        [FromQuery] string objectKey,
        CancellationToken cancellationToken)
    {
        StorageObjectId objectId =
            StorageObjectId.Create(
                container,
                objectKey);

        await _storage.DeleteAsync(
            objectId,
            cancellationToken);

        return NoContent();
    }

    private static TimeSpan ValidateExpiry(
        int expiryMinutes)
    {
        if (expiryMinutes is < 1 or > 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiryMinutes),
                "Expiry must be between 1 and 60 minutes.");
        }

        return TimeSpan.FromMinutes(expiryMinutes);
    }

}
