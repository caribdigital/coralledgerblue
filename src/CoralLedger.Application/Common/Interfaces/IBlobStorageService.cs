namespace CoralLedger.Application.Common.Interfaces;

public interface IBlobStorageService
{
    bool IsConfigured { get; }

    Task<BlobUploadResult> UploadPhotoAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream?> DownloadPhotoAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    Task<bool> DeletePhotoAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    string GetPhotoUrl(string blobName);
}

public record BlobUploadResult(
    bool Success,
    string? BlobName = null,
    string? BlobUri = null,
    long? FileSizeBytes = null,
    string? Error = null);
