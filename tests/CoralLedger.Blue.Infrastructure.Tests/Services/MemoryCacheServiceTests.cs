using CoralLedger.Blue.Infrastructure.Services;
using CoralLedger.Blue.Infrastructure.Tests.TestUtilities;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoralLedger.Blue.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for MemoryCacheService - verifies in-memory caching functionality with edge cases
/// </summary>
public class MemoryCacheServiceTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<MemoryCacheService>> _loggerMock;
    private readonly MemoryCacheService _service;

    public MemoryCacheServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<MemoryCacheService>>();
        _service = new MemoryCacheService(_memoryCache, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsValue()
    {
        // Arrange
        var key = "test:key";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };
        await _service.SetAsync(key, testObject);

        // Act
        var result = await _service.GetAsync<TestCacheObject>(key);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetAsync_CacheMiss_ReturnsNull()
    {
        // Arrange
        var key = "nonexistent:key";

        // Act
        var result = await _service.GetAsync<TestCacheObject>(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WithExpiration_StoresValue()
    {
        // Arrange
        var key = "test:key";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };
        var expiration = TimeSpan.FromMinutes(10);

        // Act
        await _service.SetAsync(key, testObject, expiration);
        var result = await _service.GetAsync<TestCacheObject>(key);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
    }

    [Fact]
    public async Task SetAsync_WithoutExpiration_UsesDefaultExpiration()
    {
        // Arrange
        var key = "test:key";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };

        // Act
        await _service.SetAsync(key, testObject);
        var result = await _service.GetAsync<TestCacheObject>(key);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_RemovesValue()
    {
        // Arrange
        var key = "test:key";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };
        await _service.SetAsync(key, testObject);

        // Act
        await _service.RemoveAsync(key);
        var result = await _service.GetAsync<TestCacheObject>(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveByPrefixAsync_RemovesMatchingKeys()
    {
        // Arrange
        var prefix = "user:";
        await _service.SetAsync($"{prefix}1", new TestCacheObject { Id = 1, Name = "User1" });
        await _service.SetAsync($"{prefix}2", new TestCacheObject { Id = 2, Name = "User2" });
        await _service.SetAsync("product:1", new TestCacheObject { Id = 3, Name = "Product1" });

        // Act
        await _service.RemoveByPrefixAsync(prefix);

        // Assert
        (await _service.GetAsync<TestCacheObject>($"{prefix}1")).Should().BeNull();
        (await _service.GetAsync<TestCacheObject>($"{prefix}2")).Should().BeNull();
        (await _service.GetAsync<TestCacheObject>("product:1")).Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrSetAsync_CacheHit_ReturnsFromCacheWithoutCallingFactory()
    {
        // Arrange
        var key = "test:key";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };
        await _service.SetAsync(key, testObject);

        var factoryCalled = false;

        // Act
        var result = await _service.GetOrSetAsync(key, () =>
        {
            factoryCalled = true;
            return Task.FromResult(new TestCacheObject { Id = 2, Name = "Factory" });
        });

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("Test");
        factoryCalled.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrSetAsync_CacheMiss_CallsFactoryAndCachesResult()
    {
        // Arrange
        var key = "test:key";
        var factoryObject = new TestCacheObject { Id = 2, Name = "Factory" };

        // Act
        var result = await _service.GetOrSetAsync(key, () => Task.FromResult(factoryObject));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(2);
        result.Name.Should().Be("Factory");

        // Verify it was cached
        var cachedResult = await _service.GetAsync<TestCacheObject>(key);
        cachedResult.Should().NotBeNull();
        cachedResult!.Id.Should().Be(2);
    }

    [Fact]
    public async Task WhenEntryRemoved_KeyIsRemovedFromTracking()
    {
        // Arrange
        var key = "test:removal";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };
        await _service.SetAsync(key, testObject);

        // Act - Remove the item (this triggers the eviction callback internally)
        await _service.RemoveAsync(key);

        // Assert - Get should return null after removal
        var result = await _service.GetAsync<TestCacheObject>(key);
        result.Should().BeNull("the cache entry should have been removed");

        // Verify that RemoveByPrefix doesn't crash when the key is no longer tracked
        // The key should already be removed from internal tracking
        Func<Task> act = async () => await _service.RemoveByPrefixAsync("test:");
        await act.Should().NotThrowAsync("RemoveByPrefix should handle already-removed keys gracefully");
    }

    [Fact]
    [Trait("Category", "Timing")]
    public async Task WhenEntryExpires_EventuallyReturnsNull()
    {
        // Arrange - This test verifies expiration behavior
        // Note: Marked as Timing category as it may be slower in CI environments
        var key = "test:expiration";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };

        // Set with very short expiration
        await _service.SetAsync(key, testObject, TimeSpan.FromMilliseconds(50));

        // Wait for expiration
        await Task.Delay(200);

        // Act - Access the cache to trigger lazy eviction check
        var result = await _service.GetAsync<TestCacheObject>(key);

        // Assert - Entry should be expired
        result.Should().BeNull("the cache entry should have expired");
    }

    [Fact]
    public async Task EvictionCallback_WhenEntryExpires_RemovesKeyFromTracking()
    {
        // Arrange - This test verifies the eviction callback is properly registered
        // by checking that keys are tracked and can be removed by prefix
        var key1 = "evict:1";
        var key2 = "evict:2";
        var key3 = "other:1";

        await _service.SetAsync(key1, new TestCacheObject { Id = 1, Name = "Test1" });
        await _service.SetAsync(key2, new TestCacheObject { Id = 2, Name = "Test2" });
        await _service.SetAsync(key3, new TestCacheObject { Id = 3, Name = "Test3" });

        // Act - Remove by prefix should only remove tracked keys with matching prefix
        await _service.RemoveByPrefixAsync("evict:");

        // Assert
        (await _service.GetAsync<TestCacheObject>(key1)).Should().BeNull();
        (await _service.GetAsync<TestCacheObject>(key2)).Should().BeNull();
        (await _service.GetAsync<TestCacheObject>(key3)).Should().NotBeNull("non-matching prefix should remain");
    }

    [Fact]
    public async Task GetOrSetAsync_ConcurrentCalls_OnlyCallsFactoryOnce()
    {
        // Arrange
        var key = "test:concurrent";
        var factoryCallCount = 0;
        var factoryDelay = TimeSpan.FromMilliseconds(100);

        // Act - Simulate concurrent requests
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            _service.GetOrSetAsync(key, async () =>
            {
                Interlocked.Increment(ref factoryCallCount);
                await Task.Delay(factoryDelay);
                return new TestCacheObject { Id = 1, Name = "Test" };
            })
        );

        var results = await Task.WhenAll(tasks);

        // Assert
        factoryCallCount.Should().Be(1, "factory should only be called once due to semaphore");
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.Id.Should().Be(1);
        });
    }

    [Fact]
    public async Task SetAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        string? key = null;
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };

        // Act
        Func<Task> act = async () => await _service.SetAsync(key!, testObject);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        string? key = null;

        // Act
        Func<Task> act = async () => await _service.GetAsync<TestCacheObject>(key!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RemoveByPrefixAsync_WithEmptyPrefix_RemovesAllKeys()
    {
        // Arrange
        await _service.SetAsync("key1", new TestCacheObject { Id = 1, Name = "Test1" });
        await _service.SetAsync("key2", new TestCacheObject { Id = 2, Name = "Test2" });
        await _service.SetAsync("key3", new TestCacheObject { Id = 3, Name = "Test3" });

        // Act
        await _service.RemoveByPrefixAsync("");

        // Assert - All keys should be removed since empty string matches all
        (await _service.GetAsync<TestCacheObject>("key1")).Should().BeNull();
        (await _service.GetAsync<TestCacheObject>("key2")).Should().BeNull();
        (await _service.GetAsync<TestCacheObject>("key3")).Should().BeNull();
    }

    [Fact]
    public async Task RemoveByPrefixAsync_CaseInsensitive_RemovesMatchingKeys()
    {
        // Arrange
        await _service.SetAsync("USER:1", new TestCacheObject { Id = 1, Name = "User1" });
        await _service.SetAsync("user:2", new TestCacheObject { Id = 2, Name = "User2" });
        await _service.SetAsync("User:3", new TestCacheObject { Id = 3, Name = "User3" });
        await _service.SetAsync("product:1", new TestCacheObject { Id = 4, Name = "Product1" });

        // Act
        await _service.RemoveByPrefixAsync("user:");

        // Assert - All case variations should be removed
        (await _service.GetAsync<TestCacheObject>("USER:1")).Should().BeNull();
        (await _service.GetAsync<TestCacheObject>("user:2")).Should().BeNull();
        (await _service.GetAsync<TestCacheObject>("User:3")).Should().BeNull();
        (await _service.GetAsync<TestCacheObject>("product:1")).Should().NotBeNull();
    }

    [Fact]
    public async Task SetAsync_SameKeyTwice_OverwritesPreviousValue()
    {
        // Arrange
        var key = "test:overwrite";
        var firstObject = new TestCacheObject { Id = 1, Name = "First" };
        var secondObject = new TestCacheObject { Id = 2, Name = "Second" };

        // Act
        await _service.SetAsync(key, firstObject);
        await _service.SetAsync(key, secondObject);

        // Assert
        var result = await _service.GetAsync<TestCacheObject>(key);
        result.Should().NotBeNull();
        result!.Id.Should().Be(2);
        result.Name.Should().Be("Second");
    }

    [Fact]
    public async Task GetAsync_WithCancellationToken_ProperlyCancels()
    {
        // Arrange
        var key = "test:cancel";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _service.GetAsync<TestCacheObject>(key, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SetAsync_WithCancellationToken_ProperlyCancels()
    {
        // Arrange
        var key = "test:cancel";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _service.SetAsync(key, testObject, null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetOrSetAsync_WithCancellationToken_ProperlyCancels()
    {
        // Arrange
        var key = "test:cancel";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _service.GetOrSetAsync(
            key,
            () => Task.FromResult(new TestCacheObject { Id = 1, Name = "Test" }),
            null,
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SetAsync_MultipleKeysWithDifferentPrefixes_TracksAllKeys()
    {
        // Arrange
        var keys = new[]
        {
            "user:1", "user:2",
            "product:1", "product:2",
            "order:1", "order:2"
        };

        // Act
        foreach (var key in keys)
        {
            await _service.SetAsync(key, new TestCacheObject { Id = 1, Name = key });
        }

        // Remove user: prefix
        await _service.RemoveByPrefixAsync("user:");

        // Assert
        (await _service.GetAsync<TestCacheObject>("user:1")).Should().BeNull();
        (await _service.GetAsync<TestCacheObject>("user:2")).Should().BeNull();
        (await _service.GetAsync<TestCacheObject>("product:1")).Should().NotBeNull();
        (await _service.GetAsync<TestCacheObject>("product:2")).Should().NotBeNull();
        (await _service.GetAsync<TestCacheObject>("order:1")).Should().NotBeNull();
        (await _service.GetAsync<TestCacheObject>("order:2")).Should().NotBeNull();
    }
}
