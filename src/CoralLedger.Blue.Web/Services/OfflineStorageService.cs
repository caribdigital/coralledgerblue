using Microsoft.JSInterop;
using System.Text.Json;

namespace CoralLedger.Blue.Web.Services;

/// <summary>
/// Blazor service for offline observation draft storage using IndexedDB.
/// Sprint 4.2 US-4.2.5: Enable offline draft saving and sync when connectivity returns.
/// </summary>
public class OfflineStorageService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<OfflineStorageService> _logger;
    private bool _initialized;

    public OfflineStorageService(IJSRuntime jsRuntime, ILogger<OfflineStorageService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the IndexedDB database
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("offlineStorage.initializeDatabase");
            _initialized = true;
            _logger.LogInformation("Offline storage initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize offline storage");
            throw;
        }
    }

    /// <summary>
    /// Save an observation draft locally
    /// </summary>
    public async Task<string> SaveDraftAsync(ObservationDraft draft)
    {
        await InitializeAsync();

        var draftId = await _jsRuntime.InvokeAsync<string>("offlineStorage.saveDraft", draft);
        _logger.LogDebug("Draft saved: {DraftId}", draftId);
        return draftId;
    }

    /// <summary>
    /// Get a draft by ID
    /// </summary>
    public async Task<ObservationDraft?> GetDraftAsync(string draftId)
    {
        await InitializeAsync();

        var draft = await _jsRuntime.InvokeAsync<ObservationDraft?>("offlineStorage.getDraft", draftId);
        return draft;
    }

    /// <summary>
    /// Get all drafts
    /// </summary>
    public async Task<List<ObservationDraft>> GetAllDraftsAsync()
    {
        await InitializeAsync();

        var drafts = await _jsRuntime.InvokeAsync<List<ObservationDraft>>("offlineStorage.getAllDrafts");
        return drafts ?? new List<ObservationDraft>();
    }

    /// <summary>
    /// Delete a draft and its photos
    /// </summary>
    public async Task DeleteDraftAsync(string draftId)
    {
        await InitializeAsync();

        await _jsRuntime.InvokeVoidAsync("offlineStorage.deleteDraft", draftId);
        _logger.LogDebug("Draft deleted: {DraftId}", draftId);
    }

    /// <summary>
    /// Save a photo for a draft
    /// </summary>
    public async Task<string> SavePhotoAsync(string draftId, Stream photoStream, string fileName, PhotoMetadata? metadata = null)
    {
        await InitializeAsync();

        // Read stream to byte array for JS interop
        using var ms = new MemoryStream();
        await photoStream.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var photoId = await _jsRuntime.InvokeAsync<string>(
            "offlineStorage.savePhoto",
            draftId,
            new ByteArrayContent(bytes),
            fileName,
            metadata ?? new PhotoMetadata());

        _logger.LogDebug("Photo saved: {PhotoId} for draft: {DraftId}", photoId, draftId);
        return photoId;
    }

    /// <summary>
    /// Get photos for a draft
    /// </summary>
    public async Task<List<DraftPhoto>> GetPhotosForDraftAsync(string draftId)
    {
        await InitializeAsync();

        var photos = await _jsRuntime.InvokeAsync<List<DraftPhoto>>("offlineStorage.getPhotosForDraft", draftId);
        return photos ?? new List<DraftPhoto>();
    }

    /// <summary>
    /// Queue a draft for synchronization when online
    /// </summary>
    public async Task QueueForSyncAsync(string draftId)
    {
        await InitializeAsync();

        await _jsRuntime.InvokeVoidAsync("offlineStorage.queueForSync", draftId);
        _logger.LogDebug("Draft queued for sync: {DraftId}", draftId);
    }

    /// <summary>
    /// Get pending sync items
    /// </summary>
    public async Task<List<SyncQueueItem>> GetPendingSyncItemsAsync()
    {
        await InitializeAsync();

        var items = await _jsRuntime.InvokeAsync<List<SyncQueueItem>>("offlineStorage.getPendingSyncItems");
        return items ?? new List<SyncQueueItem>();
    }

    /// <summary>
    /// Mark a sync item as completed
    /// </summary>
    public async Task MarkSyncCompletedAsync(int queueId)
    {
        await InitializeAsync();
        await _jsRuntime.InvokeVoidAsync("offlineStorage.markSyncCompleted", queueId);
    }

    /// <summary>
    /// Mark a sync item as failed
    /// </summary>
    public async Task MarkSyncFailedAsync(int queueId, string error)
    {
        await InitializeAsync();
        await _jsRuntime.InvokeVoidAsync("offlineStorage.markSyncFailed", queueId, error);
    }

    /// <summary>
    /// Get storage statistics
    /// </summary>
    public async Task<StorageStats> GetStorageStatsAsync()
    {
        await InitializeAsync();

        var stats = await _jsRuntime.InvokeAsync<StorageStats>("offlineStorage.getStorageStats");
        return stats ?? new StorageStats();
    }

    /// <summary>
    /// Clear all offline data
    /// </summary>
    public async Task ClearAllDataAsync()
    {
        await InitializeAsync();

        await _jsRuntime.InvokeVoidAsync("offlineStorage.clearAllData");
        _logger.LogInformation("All offline data cleared");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Wrapper for byte array to pass to JS
/// </summary>
public class ByteArrayContent
{
    public byte[] Data { get; }

    public ByteArrayContent(byte[] data)
    {
        Data = data;
    }
}

/// <summary>
/// Observation draft model for offline storage
/// </summary>
public record ObservationDraft
{
    public string? DraftId { get; init; }
    public double Longitude { get; init; }
    public double Latitude { get; init; }
    public string ObservationType { get; init; } = string.Empty;
    public string? ObservationTime { get; init; }
    public string? Notes { get; init; }
    public double? DepthMeters { get; init; }
    public string? SpeciesId { get; init; }
    public List<string> PhotoIds { get; init; } = new();
    public string Status { get; init; } = "draft";
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Photo metadata for EXIF GPS information
/// </summary>
public record PhotoMetadata
{
    public double? ExifLongitude { get; init; }
    public double? ExifLatitude { get; init; }
    public double? ExifAltitude { get; init; }
    public string? ExifTimestamp { get; init; }
}

/// <summary>
/// Draft photo model
/// </summary>
public record DraftPhoto
{
    public string PhotoId { get; init; } = string.Empty;
    public string DraftId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long Size { get; init; }
    public PhotoMetadata? Metadata { get; init; }
    public string? CreatedAt { get; init; }
}

/// <summary>
/// Sync queue item model
/// </summary>
public record SyncQueueItem
{
    public int QueueId { get; init; }
    public string DraftId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? QueuedAt { get; init; }
    public int Attempts { get; init; }
    public string? LastError { get; init; }
    public string? LastAttemptAt { get; init; }
}

/// <summary>
/// Storage statistics model
/// </summary>
public record StorageStats
{
    public int DraftCount { get; init; }
    public int PendingSyncCount { get; init; }
    public long TotalPhotoSize { get; init; }
    public string TotalPhotoSizeMB { get; init; } = "0";
}
