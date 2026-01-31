using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CoralLedger.Blue.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoralLedger.Blue.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobStorageOptions _options;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly BlobContainerClient? _containerClient;

    public BlobStorageService(
        IOptions<BlobStorageOptions> options,
        ILogger<BlobStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.Enabled && !string.IsNullOrEmpty(_options.ConnectionString))
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(_options.ConnectionString);
                _containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Azure Blob Storage client");
            }
        }
    }

    public bool IsConfigured => _options.Enabled && _containerClient != null;

    public async Task<BlobUploadResult> UploadPhotoAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new BlobUploadResult(false, Error: "Blob storage is not configured");
        }

        try
        {
            // Ensure container exists
            await _containerClient!.CreateIfNotExistsAsync(
                PublicAccessType.Blob,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Generate unique blob name with timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var extension = Path.GetExtension(fileName);
            var blobName = $"{timestamp}_{Guid.NewGuid():N}{extension}";

            var blobClient = _containerClient.GetBlobClient(blobName);

            var headers = new BlobHttpHeaders
            {
                ContentType = contentType
            };

            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions { HttpHeaders = headers },
                cancellationToken).ConfigureAwait(false);

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Uploaded photo {BlobName} ({Size} bytes)",
                blobName, properties.Value.ContentLength);

            return new BlobUploadResult(
                Success: true,
                BlobName: blobName,
                BlobUri: blobClient.Uri.ToString(),
                FileSizeBytes: properties.Value.ContentLength);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload photo {FileName}", fileName);
            return new BlobUploadResult(false, Error: ex.Message);
        }
    }

    public async Task<Stream?> DownloadPhotoAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Blob storage not configured for download");
            return null;
        }

        try
        {
            var blobClient = _containerClient!.GetBlobClient(blobName);
            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download photo {BlobName}", blobName);
            return null;
        }
    }

    public async Task<bool> DeletePhotoAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        try
        {
            var blobClient = _containerClient!.GetBlobClient(blobName);
            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Value)
            {
                _logger.LogInformation("Deleted photo {BlobName}", blobName);
            }

            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete photo {BlobName}", blobName);
            return false;
        }
    }

    public string GetPhotoUrl(string blobName)
    {
        if (!IsConfigured)
        {
            return string.Empty;
        }

        var blobClient = _containerClient!.GetBlobClient(blobName);
        return blobClient.Uri.ToString();
    }
}
