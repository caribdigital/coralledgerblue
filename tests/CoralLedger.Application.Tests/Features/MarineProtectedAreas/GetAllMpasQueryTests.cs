using CoralLedger.Application.Common.Interfaces;
using CoralLedger.Application.Features.MarineProtectedAreas.Queries.GetAllMpas;
using CoralLedger.Domain.Entities;
using CoralLedger.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace CoralLedger.Application.Tests.Features.MarineProtectedAreas;

public class GetAllMpasQueryTests
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    private static Polygon CreateTestPolygon(double centerLon = -77.5, double centerLat = 24.5, double size = 0.1)
    {
        var coordinates = new[]
        {
            new Coordinate(centerLon - size, centerLat - size),
            new Coordinate(centerLon + size, centerLat - size),
            new Coordinate(centerLon + size, centerLat + size),
            new Coordinate(centerLon - size, centerLat + size),
            new Coordinate(centerLon - size, centerLat - size)
        };
        return GeometryFactory.CreatePolygon(coordinates);
    }

    private static MarineProtectedArea CreateTestMpa(
        string name,
        ProtectionLevel level = ProtectionLevel.NoTake,
        IslandGroup group = IslandGroup.Exumas)
    {
        return MarineProtectedArea.Create(
            name,
            CreateTestPolygon(),
            level,
            group);
    }

    private static Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();

        mockSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(data.GetEnumerator()));

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));

        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());

        return mockSet;
    }

    [Fact]
    public async Task Handle_WithMultipleMpas_ReturnsAllMpas()
    {
        // Arrange
        var mpas = new List<MarineProtectedArea>
        {
            CreateTestMpa("Exuma Cays Land and Sea Park", ProtectionLevel.NoTake, IslandGroup.Exumas),
            CreateTestMpa("Andros West Side National Park", ProtectionLevel.HighlyProtected, IslandGroup.Andros),
            CreateTestMpa("Lucayan National Park", ProtectionLevel.LightlyProtected, IslandGroup.GrandBahama)
        };

        var mockDbSet = CreateMockDbSet(mpas);
        var mockContext = new Mock<IMarineDbContext>();
        mockContext.Setup(c => c.MarineProtectedAreas).Returns(mockDbSet.Object);

        var handler = new GetAllMpasQueryHandler(mockContext.Object);
        var query = new GetAllMpasQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Select(r => r.Name).Should().Contain("Exuma Cays Land and Sea Park");
        result.Select(r => r.Name).Should().Contain("Andros West Side National Park");
        result.Select(r => r.Name).Should().Contain("Lucayan National Park");
    }

    [Fact]
    public async Task Handle_WithNoMpas_ReturnsEmptyList()
    {
        // Arrange
        var mpas = new List<MarineProtectedArea>();
        var mockDbSet = CreateMockDbSet(mpas);
        var mockContext = new Mock<IMarineDbContext>();
        mockContext.Setup(c => c.MarineProtectedAreas).Returns(mockDbSet.Object);

        var handler = new GetAllMpasQueryHandler(mockContext.Object);
        var query = new GetAllMpasQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsDtoPropertiesCorrectly()
    {
        // Arrange
        var mpa = CreateTestMpa("Test MPA", ProtectionLevel.NoTake, IslandGroup.Exumas);
        var mpas = new List<MarineProtectedArea> { mpa };

        var mockDbSet = CreateMockDbSet(mpas);
        var mockContext = new Mock<IMarineDbContext>();
        mockContext.Setup(c => c.MarineProtectedAreas).Returns(mockDbSet.Object);

        var handler = new GetAllMpasQueryHandler(mockContext.Object);
        var query = new GetAllMpasQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result.First();
        dto.Id.Should().Be(mpa.Id);
        dto.Name.Should().Be("Test MPA");
        dto.ProtectionLevel.Should().Be("NoTake");
        dto.IslandGroup.Should().Be("Exumas");
        dto.AreaSquareKm.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_IncludesCentroidCoordinates()
    {
        // Arrange
        var mpa = CreateTestMpa("Centered MPA");
        var mpas = new List<MarineProtectedArea> { mpa };

        var mockDbSet = CreateMockDbSet(mpas);
        var mockContext = new Mock<IMarineDbContext>();
        mockContext.Setup(c => c.MarineProtectedAreas).Returns(mockDbSet.Object);

        var handler = new GetAllMpasQueryHandler(mockContext.Object);

        // Act
        var result = await handler.Handle(new GetAllMpasQuery(), CancellationToken.None);

        // Assert
        var dto = result.First();
        dto.CentroidLongitude.Should().BeApproximately(-77.5, 0.1);
        dto.CentroidLatitude.Should().BeApproximately(24.5, 0.1);
    }
}

// Helper classes for async LINQ queries with Moq
internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());

    public T Current => _inner.Current;
}

internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    public TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
        => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression)
        => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(System.Linq.Expressions.Expression expression)
        => _inner.Execute(expression);

    public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
        => _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethod(
                name: nameof(IQueryProvider.Execute),
                genericParameterCount: 1,
                types: new[] { typeof(System.Linq.Expressions.Expression) })!
            .MakeGenericMethod(resultType)
            .Invoke(this, new[] { expression });

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, new[] { executionResult })!;
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
    public TestAsyncEnumerable(System.Linq.Expressions.Expression expression) : base(expression) { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}
