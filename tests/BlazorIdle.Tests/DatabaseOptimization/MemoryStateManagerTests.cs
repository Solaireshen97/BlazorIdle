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
/// MemoryStateManager 单元测试
/// Unit tests for MemoryStateManager
/// </summary>
public class MemoryStateManagerTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMemoryStateManager<TestEntity> _manager;

    public MemoryStateManagerTests()
    {
        var services = new ServiceCollection();
        
        // 配置选项
        services.Configure<MemoryCacheOptions>(options =>
        {
            options.MaxCachedEntities = 10;
            options.EvictionPolicy = "LRU";
            options.TimeToLiveSeconds = 3600;
        });
        
        // 日志
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        // 添加必要的服务
        services.AddScoped<IMemoryStateManager<TestEntity>, MemoryStateManager<TestEntity>>();
        
        _serviceProvider = services.BuildServiceProvider();
        _manager = _serviceProvider.GetRequiredService<IMemoryStateManager<TestEntity>>();
    }

    [Fact]
    public void Add_ShouldAddEntityToMemory()
    {
        // Arrange
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Test" };

        // Act
        _manager.Add(entity);

        // Assert
        _manager.Count.Should().Be(1);
        _manager.DirtyCount.Should().Be(1);
    }

    [Fact]
    public void Update_ShouldMarkEntityAsDirty()
    {
        // Arrange
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Original" };
        _manager.Add(entity);
        _manager.ClearDirty(new[] { entity.Id });

        // Act
        entity.Name = "Updated";
        _manager.Update(entity);

        // Assert
        _manager.DirtyCount.Should().Be(1);
    }

    [Fact]
    public void Remove_ShouldRemoveEntityFromMemory()
    {
        // Arrange
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Test" };
        _manager.Add(entity);

        // Act
        _manager.Remove(entity.Id);

        // Assert
        _manager.Count.Should().Be(0);
        _manager.DirtyCount.Should().Be(0);
    }

    [Fact]
    public void GetDirtyEntities_ShouldReturnOnlyDirtyEntities()
    {
        // Arrange
        var entity1 = new TestEntity { Id = Guid.NewGuid(), Name = "Entity1" };
        var entity2 = new TestEntity { Id = Guid.NewGuid(), Name = "Entity2" };
        var entity3 = new TestEntity { Id = Guid.NewGuid(), Name = "Entity3" };
        
        _manager.Add(entity1);
        _manager.Add(entity2);
        _manager.Add(entity3);
        
        // 清除 entity2 的 dirty 标记
        _manager.ClearDirty(new[] { entity2.Id });

        // Act
        var dirtyEntities = _manager.GetDirtyEntities().ToList();

        // Assert
        dirtyEntities.Should().HaveCount(2);
        dirtyEntities.Should().Contain(e => e.Id == entity1.Id);
        dirtyEntities.Should().Contain(e => e.Id == entity3.Id);
        dirtyEntities.Should().NotContain(e => e.Id == entity2.Id);
    }

    [Fact]
    public void ClearDirty_ShouldClearDirtyFlags()
    {
        // Arrange
        var entity1 = new TestEntity { Id = Guid.NewGuid(), Name = "Entity1" };
        var entity2 = new TestEntity { Id = Guid.NewGuid(), Name = "Entity2" };
        
        _manager.Add(entity1);
        _manager.Add(entity2);
        
        var initialDirtyCount = _manager.DirtyCount;

        // Act
        _manager.ClearDirty(new[] { entity1.Id, entity2.Id });

        // Assert
        initialDirtyCount.Should().Be(2);
        _manager.DirtyCount.Should().Be(0);
    }

    [Fact]
    public void GetSnapshot_ShouldReturnReadOnlyCopy()
    {
        // Arrange
        var entity1 = new TestEntity { Id = Guid.NewGuid(), Name = "Entity1" };
        var entity2 = new TestEntity { Id = Guid.NewGuid(), Name = "Entity2" };
        
        _manager.Add(entity1);
        _manager.Add(entity2);

        // Act
        var snapshot = _manager.GetSnapshot();

        // Assert
        snapshot.Should().HaveCount(2);
        snapshot.Should().ContainKey(entity1.Id);
        snapshot.Should().ContainKey(entity2.Id);
    }

    [Fact]
    public void LRU_ShouldEvictOldestNonDirtyEntities_WhenExceedingMaxCapacity()
    {
        // Arrange - 添加11个实体（超过MaxCachedEntities=10）
        for (int i = 0; i < 11; i++)
        {
            var entity = new TestEntity { Id = Guid.NewGuid(), Name = $"Entity{i}" };
            _manager.Add(entity);
            
            // 前5个标记为非dirty（模拟已保存）
            if (i < 5)
            {
                _manager.ClearDirty(new[] { entity.Id });
            }
            
            Thread.Sleep(10); // 确保访问时间不同
        }

        // Act & Assert
        // 由于超过容量，应该清理了一些非dirty的老实体
        _manager.Count.Should().BeLessOrEqualTo(10);
        _manager.DirtyCount.Should().Be(6); // 后6个dirty实体应该保留
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}

/// <summary>
/// 测试用实体类
/// Test entity class
/// </summary>
public class TestEntity : IEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
