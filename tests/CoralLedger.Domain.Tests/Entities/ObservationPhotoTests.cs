using CoralLedger.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CoralLedger.Domain.Tests.Entities;

public class ObservationPhotoTests
{
    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        // Arrange
        var observationId = Guid.NewGuid();
        var blobName = "photos/observation-123.jpg";
        var blobUri = "https://storage.example.com/photos/observation-123.jpg";
        var contentType = "image/jpeg";
        var fileSize = 2048000L;

        // Act
        var photo = ObservationPhoto.Create(
            observationId,
            blobName,
            blobUri,
            contentType,
            fileSize);

        // Assert
        photo.CitizenObservationId.Should().Be(observationId);
        photo.BlobName.Should().Be(blobName);
        photo.BlobUri.Should().Be(blobUri);
        photo.ContentType.Should().Be(contentType);
        photo.FileSizeBytes.Should().Be(fileSize);
        photo.DisplayOrder.Should().Be(0);
        photo.Caption.Should().BeNull();
        photo.Id.Should().NotBeEmpty();
        photo.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithCaptionAndOrder_SetsOptionalProperties()
    {
        // Arrange
        var observationId = Guid.NewGuid();
        var caption = "Lionfish near reef edge";
        var displayOrder = 2;

        // Act
        var photo = ObservationPhoto.Create(
            observationId,
            "test.jpg",
            "https://example.com/test.jpg",
            "image/jpeg",
            1024,
            caption: caption,
            displayOrder: displayOrder);

        // Assert
        photo.Caption.Should().Be(caption);
        photo.DisplayOrder.Should().Be(displayOrder);
    }

    [Fact]
    public void UpdateCaption_ChangesCaption()
    {
        // Arrange
        var photo = ObservationPhoto.Create(
            Guid.NewGuid(),
            "test.jpg",
            "https://example.com/test.jpg",
            "image/jpeg",
            1024,
            caption: "Original caption");

        // Act
        photo.UpdateCaption("Updated caption");

        // Assert
        photo.Caption.Should().Be("Updated caption");
    }

    [Fact]
    public void UpdateCaption_CanSetToNull()
    {
        // Arrange
        var photo = ObservationPhoto.Create(
            Guid.NewGuid(),
            "test.jpg",
            "https://example.com/test.jpg",
            "image/jpeg",
            1024,
            caption: "Original caption");

        // Act
        photo.UpdateCaption(null);

        // Assert
        photo.Caption.Should().BeNull();
    }

    [Fact]
    public void UpdateDisplayOrder_ChangesOrder()
    {
        // Arrange
        var photo = ObservationPhoto.Create(
            Guid.NewGuid(),
            "test.jpg",
            "https://example.com/test.jpg",
            "image/jpeg",
            1024,
            displayOrder: 0);

        // Act
        photo.UpdateDisplayOrder(5);

        // Assert
        photo.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Arrange
        var observationId = Guid.NewGuid();

        // Act
        var photo1 = ObservationPhoto.Create(observationId, "test1.jpg", "uri1", "image/jpeg", 1000);
        var photo2 = ObservationPhoto.Create(observationId, "test2.jpg", "uri2", "image/jpeg", 2000);

        // Assert
        photo1.Id.Should().NotBe(photo2.Id);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/heic")]
    public void Create_SupportsVariousContentTypes(string contentType)
    {
        // Act
        var photo = ObservationPhoto.Create(
            Guid.NewGuid(),
            "test.img",
            "https://example.com/test.img",
            contentType,
            1024);

        // Assert
        photo.ContentType.Should().Be(contentType);
    }

    [Fact]
    public void Create_SupportsLargeFileSizes()
    {
        // Arrange - 50MB file
        var largeFileSize = 50 * 1024 * 1024L;

        // Act
        var photo = ObservationPhoto.Create(
            Guid.NewGuid(),
            "large-photo.jpg",
            "https://example.com/large-photo.jpg",
            "image/jpeg",
            largeFileSize);

        // Assert
        photo.FileSizeBytes.Should().Be(largeFileSize);
    }
}
