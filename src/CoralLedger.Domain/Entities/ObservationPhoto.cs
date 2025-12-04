using CoralLedger.Domain.Common;

namespace CoralLedger.Domain.Entities;

public class ObservationPhoto : BaseEntity
{
    public string BlobName { get; private set; } = string.Empty;
    public string BlobUri { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string? Caption { get; private set; }
    public int DisplayOrder { get; private set; }

    public DateTime UploadedAt { get; private set; }

    // Foreign key
    public Guid CitizenObservationId { get; private set; }
    public CitizenObservation CitizenObservation { get; private set; } = null!;

    private ObservationPhoto() { }

    public static ObservationPhoto Create(
        Guid observationId,
        string blobName,
        string blobUri,
        string contentType,
        long fileSizeBytes,
        string? caption = null,
        int displayOrder = 0)
    {
        return new ObservationPhoto
        {
            Id = Guid.NewGuid(),
            CitizenObservationId = observationId,
            BlobName = blobName,
            BlobUri = blobUri,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            Caption = caption,
            DisplayOrder = displayOrder,
            UploadedAt = DateTime.UtcNow
        };
    }

    public void UpdateCaption(string? caption)
    {
        Caption = caption;
    }

    public void UpdateDisplayOrder(int order)
    {
        DisplayOrder = order;
    }
}
