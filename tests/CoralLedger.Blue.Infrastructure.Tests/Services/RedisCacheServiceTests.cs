using CoralLedger.Blue.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace CoralLedger.Blue.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for RedisCacheService - verifies distributed caching functionality
/// </summary>
public class RedisCacheServiceTests
{
    private readonly Mock<IDistributedCache> _distributedCacheMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<ILogger<RedisCacheService>> _loggerMock;
    private readonly RedisCacheService _service;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    public RedisCacheServiceTests()
    {
        _distributedCacheMock = new Mock<IDistributedCache>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _loggerMock = new Mock<ILogger<RedisCacheService>>();
        _service = new RedisCacheService(_distributedCacheMock.Object, _loggerMock.Object, _redisMock.Object);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var key = "test:key";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };
        var json = System.Text.Json.JsonSerializer.Serialize(testObject, JsonOptions);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        _distributedCacheMock
            .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

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
        var key = "test:key";
        _distributedCacheMock
            .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _service.GetAsync<TestCacheObject>(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WithExpiration_StoresSerializedValue()
    {
        // Arrange
        var key = "test:key";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };
        var expiration = TimeSpan.FromMinutes(10);
        byte[]? capturedBytes = null;
        DistributedCacheEntryOptions? capturedOptions = null;

        _distributedCacheMock
            .Setup(c => c.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (k, bytes, options, ct) =>
                {
                    capturedBytes = bytes;
                    capturedOptions = options;
                })
            .Returns(Task.CompletedTask);

        // Act
        await _service.SetAsync(key, testObject, expiration);

        // Assert
        capturedBytes.Should().NotBeNull();
        capturedOptions.Should().NotBeNull();
        capturedOptions!.AbsoluteExpirationRelativeToNow.Should().Be(expiration);

        var json = System.Text.Encoding.UTF8.GetString(capturedBytes!);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TestCacheObject>(json, JsonOptions);
        deserialized!.Id.Should().Be(1);
        deserialized.Name.Should().Be("Test");
    }

    [Fact]
    public async Task SetAsync_WithoutExpiration_UsesDefaultExpiration()
    {
        // Arrange
        var key = "test:key";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };
        DistributedCacheEntryOptions? capturedOptions = null;

        _distributedCacheMock
            .Setup(c => c.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (k, bytes, options, ct) => capturedOptions = options)
            .Returns(Task.CompletedTask);

        // Act
        await _service.SetAsync(key, testObject);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task RemoveAsync_CallsDistributedCacheRemove()
    {
        // Arrange
        var key = "test:key";

        // Act
        await _service.RemoveAsync(key);

        // Assert
        _distributedCacheMock.Verify(
            c => c.RemoveAsync(key, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrSetAsync_CacheHit_ReturnsFromCacheWithoutCallingFactory()
    {
        // Arrange
        var key = "test:key";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };
        var json = System.Text.Json.JsonSerializer.Serialize(testObject, JsonOptions);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        _distributedCacheMock
            .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

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

        _distributedCacheMock
            .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _distributedCacheMock
            .Setup(c => c.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GetOrSetAsync(key, () => Task.FromResult(factoryObject));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(2);
        result.Name.Should().Be("Factory");

        _distributedCacheMock.Verify(
            c => c.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_OnException_ReturnsNull()
    {
        // Arrange
        var key = "test:key";
        _distributedCacheMock
            .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection failed"));

        // Act
        var result = await _service.GetAsync<TestCacheObject>(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_OnException_DoesNotThrow()
    {
        // Arrange
        var key = "test:key";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };

        _distributedCacheMock
            .Setup(c => c.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection failed"));

        // Act
        Func<Task> act = async () => await _service.SetAsync(key, testObject);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveAsync_OnException_DoesNotThrow()
    {
        // Arrange
        var key = "test:key";
        _distributedCacheMock
            .Setup(c => c.RemoveAsync(key, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection failed"));

        // Act
        Func<Task> act = async () => await _service.RemoveAsync(key);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveByPrefixAsync_WithNoRedisMultiplexer_LogsWarning()
    {
        // Arrange
        var serviceWithoutRedis = new RedisCacheService(
            _distributedCacheMock.Object,
            _loggerMock.Object,
            redis: null);
        var prefix = "test:";

        // Act
        await serviceWithoutRedis.RemoveByPrefixAsync(prefix);

        // Assert - Should log warning about missing IConnectionMultiplexer
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("IConnectionMultiplexer not available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_WithNoEndpoints_LogsWarning()
    {
        // Arrange
        var prefix = "test:";
        _redisMock.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Returns(Array.Empty<System.Net.EndPoint>());

        // Act
        await _service.RemoveByPrefixAsync(prefix);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No Redis endpoints available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_WhenAllServersDisconnected_LogsWarning()
    {
        // Arrange
        var prefix = "test:";
        var endpointMock = new Mock<System.Net.EndPoint>();
        var serverMock = new Mock<IServer>();
        
        serverMock.Setup(s => s.IsConnected).Returns(false);
        
        _redisMock.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Returns(new[] { endpointMock.Object });
        _redisMock.Setup(r => r.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>()))
            .Returns(serverMock.Object);

        // Act
        await _service.RemoveByPrefixAsync(prefix);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No available Redis server found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_OnException_LogsError()
    {
        // Arrange
        var prefix = "test:";
        _redisMock.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Throws(new Exception("Redis connection failed"));

        // Act
        await _service.RemoveByPrefixAsync(prefix);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error removing cache entries with prefix")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrSetAsync_FactoryThrows_PropagatesException()
    {
        // Arrange
        var key = "test:key";
        var expectedException = new InvalidOperationException("Factory failed");

        _distributedCacheMock
            .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        Func<Task> act = async () => await _service.GetOrSetAsync<TestCacheObject>(key, () => throw expectedException);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Factory failed");
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
    public async Task GetOrSetAsync_MultipleCalls_ReturnsConsistentValues()
    {
        // Arrange
        var key = "test:concurrent";
        var testObject = new TestCacheObject { Id = 1, Name = "Test" };
        var json = System.Text.Json.JsonSerializer.Serialize(testObject, JsonOptions);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        // First call - cache miss, subsequent calls - cache hit
        _distributedCacheMock
            .SetupSequence(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null)  // First check - miss
            .ReturnsAsync(bytes)           // Subsequent checks - hit
            .ReturnsAsync(bytes)
            .ReturnsAsync(bytes)
            .ReturnsAsync(bytes);

        _distributedCacheMock
            .Setup(c => c.SetAsync(
                key,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - Simulate sequential calls (RedisCacheService doesn't have semaphore protection)
        var results = new List<TestCacheObject>();
        for (int i = 0; i < 5; i++)
        {
            var result = await _service.GetOrSetAsync(key, async () =>
            {
                await Task.Delay(10);
                return new TestCacheObject { Id = 1, Name = "Test" };
            });
            results.Add(result);
        }

        // Assert - All should get the same consistent value
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.Id.Should().Be(1);
        });
    }

    [Fact]
    public async Task GetAsync_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var key = "test:invalid";
        var invalidJson = System.Text.Encoding.UTF8.GetBytes("{ invalid json }");

        _distributedCacheMock
            .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidJson);

        // Act
        var result = await _service.GetAsync<TestCacheObject>(key);

        // Assert
        result.Should().BeNull();
        
        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting cache value")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithNullValue_DoesNotThrow()
    {
        // Arrange
        var key = "test:null";
        TestCacheObject? nullObject = null;

        // Act
        Func<Task> act = async () => await _service.SetAsync(key, nullObject!);

        // Assert
        // SetAsync catches all exceptions and logs them, so it won't throw even if serialization fails
        await act.Should().NotThrowAsync();
    }
}

/// <summary>
/// Test object for cache serialization tests
/// </summary>
public class TestCacheObject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
