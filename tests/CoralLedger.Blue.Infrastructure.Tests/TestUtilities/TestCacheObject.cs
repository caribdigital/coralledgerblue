namespace CoralLedger.Blue.Infrastructure.Tests.TestUtilities;

/// <summary>
/// Simple test object for cache serialization tests.
/// Used by both MemoryCacheServiceTests and RedisCacheServiceTests.
/// </summary>
public class TestCacheObject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
