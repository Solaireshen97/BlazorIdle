using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using FluentAssertions;

namespace BlazorIdle.Tests.DatabaseOptimization;

/// <summary>
/// 缓存增强功能测试
/// Tests for cache enhancement features
/// </summary>
public class CacheEnhancementTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMemoryStateManager<TestEntity> _manager;
    private readonly ILogger<CacheEnhancementTests> _logger;

    public CacheEnhancementTests()
    {
        var services = new ServiceCollection();
        
        // 配置选项
        services.Configure<MemoryCacheOptions>(options =>
        {
            options.MaxCachedEntities = 10;
            options.EvictionPolicy = "LRU";
            options.TimeToLiveSeconds = 2; // 2秒TTL用于测试
        });
        
        // 日志
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        // 添加必要的服务
        services.AddScoped<IMemoryStateManager<TestEntity>, MemoryStateManager<TestEntity>>();
        
        _serviceProvider = services.BuildServiceProvider();
        _manager = _serviceProvider.GetRequiredService<IMemoryStateManager<TestEntity>>();
        _logger = _serviceProvider.GetRequiredService<ILogger<CacheEnhancementTests>>();
    }

    [Fact]
    public async Task TryGetAsync_ShouldReturnCachedEntity_WhenInMemory()
    {
        // Arrange
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Cached" };
        _manager.Add(entity);
        _manager.ClearDirty(new[] { entity.Id }); // 清除Dirty标记

        // Act
        var result = await _manager.TryGetAsync(
            entity.Id,
            async (id, ct) =>
            {
                // 这个不应该被调用，因为实体在缓存中
                Assert.Fail("Database loader should not be called");
                return null;
            }
        );

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(entity.Id);
        result.Name.Should().Be("Cached");
        
        // 验证缓存命中率
        var stats = _manager.GetCacheStatistics();
        stats.CacheHits.Should().Be(1);
        stats.CacheMisses.Should().Be(0);
        stats.HitRate.Should().Be(1.0);
    }

    [Fact]
    public async Task TryGetAsync_ShouldLoadFromDatabase_WhenNotInMemory()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var loadedFromDb = new TestEntity { Id = entityId, Name = "FromDB" };
        var dbLoaderCalled = false;

        // Act
        var result = await _manager.TryGetAsync(
            entityId,
            async (id, ct) =>
            {
                dbLoaderCalled = true;
                await Task.Delay(10, ct); // 模拟数据库查询延迟
                return loadedFromDb;
            }
        );

        // Assert
        dbLoaderCalled.Should().BeTrue("Database loader should be called for cache miss");
        result.Should().NotBeNull();
        result!.Id.Should().Be(entityId);
        result.Name.Should().Be("FromDB");
        
        // 验证实体已缓存
        _manager.Count.Should().Be(1);
        
        // 验证缓存统计
        var stats = _manager.GetCacheStatistics();
        stats.CacheMisses.Should().Be(1);
        stats.HitRate.Should().Be(0.0);
    }

    [Fact]
    public async Task TryGetAsync_ShouldCacheLoadedEntity_ForSubsequentAccess()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entity = new TestEntity { Id = entityId, Name = "Test" };
        var dbCallCount = 0;

        // Act - First access (cache miss)
        var result1 = await _manager.TryGetAsync(
            entityId,
            async (id, ct) =>
            {
                dbCallCount++;
                await Task.CompletedTask;
                return entity;
            }
        );

        // Act - Second access (cache hit)
        var result2 = await _manager.TryGetAsync(
            entityId,
            async (id, ct) =>
            {
                dbCallCount++;
                await Task.CompletedTask;
                return entity;
            }
        );

        // Assert
        dbCallCount.Should().Be(1, "Database should only be queried once");
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Id.Should().Be(result2!.Id);
        
        var stats = _manager.GetCacheStatistics();
        stats.CacheHits.Should().Be(1);
        stats.CacheMisses.Should().Be(1);
        stats.HitRate.Should().Be(0.5);
    }

    [Fact]
    public void PreloadBatch_ShouldAddEntitiesWithoutDirtyFlag()
    {
        // Arrange
        var entities = Enumerable.Range(0, 5)
            .Select(i => new TestEntity { Id = Guid.NewGuid(), Name = $"Entity{i}" })
            .ToList();

        // Act
        _manager.PreloadBatch(entities);

        // Assert
        _manager.Count.Should().Be(5);
        _manager.DirtyCount.Should().Be(0, "Preloaded entities should not be marked dirty");
    }

    [Fact]
    public async Task GetCacheHitRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Test" };
        _manager.Add(entity);
        _manager.ClearDirty(new[] { entity.Id });

        // Act - Generate some cache hits and misses
        for (int i = 0; i < 3; i++)
        {
            _ = await _manager.TryGetAsync(
                entity.Id,
                (id, ct) => Task.FromResult<TestEntity?>(null)
            ); // Cache hit
        }

        for (int i = 0; i < 7; i++)
        {
            _ = await _manager.TryGetAsync(
                Guid.NewGuid(),
                (id, ct) => Task.FromResult<TestEntity?>(null)
            ); // Cache miss
        }

        // Assert - 3 hits out of 10 total = 30%
        var hitRate = _manager.GetCacheHitRate();
        hitRate.Should().BeApproximately(0.3, 0.01);
    }

    [Fact]
    public void GetCacheStatistics_ShouldReturnCompleteInfo()
    {
        // Arrange
        var entity1 = new TestEntity { Id = Guid.NewGuid(), Name = "E1" };
        var entity2 = new TestEntity { Id = Guid.NewGuid(), Name = "E2" };
        
        _manager.Add(entity1);
        _manager.Add(entity2);
        _manager.ClearDirty(new[] { entity1.Id }); // 只清除 entity1 的 dirty

        // Act
        var stats = _manager.GetCacheStatistics();

        // Assert
        stats.EntityType.Should().Be("TestEntity");
        stats.CachedCount.Should().Be(2);
        stats.DirtyCount.Should().Be(1); // 只有 entity2 是 dirty
    }

    [Fact]
    public async Task ClearExpired_ShouldRemoveOldEntries_BasedOnTTL()
    {
        // Arrange
        var entity1 = new TestEntity { Id = Guid.NewGuid(), Name = "Old" };
        var entity2 = new TestEntity { Id = Guid.NewGuid(), Name = "Recent" };
        
        _manager.Add(entity1);
        _manager.ClearDirty(new[] { entity1.Id }); // 清除dirty，允许被清理
        
        // 等待一段时间
        await Task.Delay(2500); // 等待超过2秒TTL
        
        _manager.Add(entity2); // 添加新实体
        _manager.ClearDirty(new[] { entity2.Id });

        // Act
        var removed = _manager.ClearExpired(2); // 2秒TTL

        // Assert
        removed.Should().BeGreaterOrEqualTo(1, "At least entity1 should be removed");
        _manager.Count.Should().BeLessOrEqualTo(1, "Only recent entity should remain");
    }

    [Fact]
    public void ClearExpired_ShouldNotRemoveDirtyEntities()
    {
        // Arrange
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "DirtyOld" };
        _manager.Add(entity);
        // 不清除 Dirty 标记

        // Act - 尝试清理，即使超过TTL
        var removed = _manager.ClearExpired(0); // 0秒TTL，应该清理所有过期的

        // Assert
        removed.Should().Be(0, "Dirty entities should not be removed");
        _manager.Count.Should().Be(1);
        _manager.DirtyCount.Should().Be(1);
    }

    [Fact]
    public void InvalidateCache_ShouldRemoveSpecificEntity()
    {
        // Arrange
        var entity1 = new TestEntity { Id = Guid.NewGuid(), Name = "E1" };
        var entity2 = new TestEntity { Id = Guid.NewGuid(), Name = "E2" };
        
        _manager.Add(entity1);
        _manager.Add(entity2);
        _manager.ClearDirty(new[] { entity1.Id, entity2.Id });

        // Act
        _manager.InvalidateCache(entity1.Id);

        // Assert
        _manager.Count.Should().Be(1);
        var remaining = _manager.GetAll().Single();
        remaining.Id.Should().Be(entity2.Id);
    }

    [Fact]
    public void InvalidateCache_ShouldNotRemoveDirtyEntity()
    {
        // Arrange
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Dirty" };
        _manager.Add(entity);
        // 不清除 Dirty 标记

        // Act
        _manager.InvalidateCache(entity.Id);

        // Assert
        _manager.Count.Should().Be(1, "Dirty entity should not be removed");
        _manager.DirtyCount.Should().Be(1);
    }

    [Fact]
    public void ClearAll_ShouldRemoveAllNonDirtyEntities()
    {
        // Arrange
        var entity1 = new TestEntity { Id = Guid.NewGuid(), Name = "Clean" };
        var entity2 = new TestEntity { Id = Guid.NewGuid(), Name = "Dirty" };
        
        _manager.Add(entity1);
        _manager.Add(entity2);
        _manager.ClearDirty(new[] { entity1.Id }); // 只清除 entity1

        // Act
        _manager.ClearAll();

        // Assert - 缓存已清空，但 Dirty 标记仍然保留
        _manager.Count.Should().Be(0);
        _manager.DirtyCount.Should().Be(1, "Dirty count should be preserved");
    }

    [Fact]
    public void GetAll_ShouldReturnAllCachedEntities()
    {
        // Arrange
        var entities = Enumerable.Range(0, 3)
            .Select(i => new TestEntity { Id = Guid.NewGuid(), Name = $"E{i}" })
            .ToList();
        
        foreach (var entity in entities)
        {
            _manager.Add(entity);
        }

        // Act
        var all = _manager.GetAll().ToList();

        // Assert
        all.Should().HaveCount(3);
        all.Select(e => e.Name).Should().BeEquivalentTo("E0", "E1", "E2");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
