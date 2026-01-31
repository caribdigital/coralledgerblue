namespace CoralLedger.Blue.Application.Common.Interfaces;

public interface IBlobStorageService
{
    bool IsConfigured { get; }

    Task<BlobUploadResult> UploadPhotoAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Download a photo from blob storage.
    /// Returns null if photo not found or on error (errors are logged).
    /// </summary>
    /// <remarks>
    /// Note: Returns null on both "not found" and "error" for simplicity.
    /// Errors are logged. Consider using ServiceResult pattern for distinguishing these cases if needed.
    /// </remarks>
    Task<Stream?> DownloadPhotoAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a photo from blob storage.
    /// Returns false if photo not found/deleted or on error (errors are logged).
    /// </summary>
    /// <remarks>
    /// Note: Returns false on both "not found" and "error" for simplicity.
    /// Errors are logged. Consider using ServiceResult pattern for distinguishing these cases if needed.
    /// </remarks>
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
