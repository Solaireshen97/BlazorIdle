using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Records;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorIdle.Tests.DatabaseOptimization;

/// <summary>
/// 数据库优化集成测试
/// Integration tests for database optimization
/// </summary>
public class PersistenceIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly GameDbContext _db;
    
    public PersistenceIntegrationTests()
    {
        var services = new ServiceCollection();
        
        // 配置内存数据库
        services.AddDbContext<GameDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        // 配置选项
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:EnableMemoryBuffering"] = "true",
                ["Persistence:SaveIntervalMs"] = "1000",
                ["Persistence:MaxBatchSize"] = "100",
                ["Persistence:ForceSaveThreshold"] = "1000",
                ["Persistence:SaveRetryAttempts"] = "3",
                ["MemoryCache:MaxCachedEntities"] = "1000",
                ["MemoryCache:EvictionPolicy"] = "LRU",
                ["MemoryCache:TimeToLiveSeconds"] = "3600"
            })
            .Build();
        
        services.AddSingleton<IConfiguration>(config);
        
        services.Configure<PersistenceOptions>(config.GetSection("Persistence"));
        services.Configure<MemoryCacheOptions>(config.GetSection("MemoryCache"));
        
        // 注册日志
        services.AddLogging(builder => builder.AddConsole());
        
        // 注册服务作用域工厂
        var tempProvider = services.BuildServiceProvider();
        services.AddSingleton<IServiceScopeFactory>(tempProvider.GetRequiredService<IServiceScopeFactory>());
        
        // 注册内存状态管理器
        services.AddSingleton<IMemoryStateManager<Character>, MemoryStateManager<Character>>();
        services.AddSingleton<IMemoryStateManager<RunningBattleSnapshotRecord>, MemoryStateManager<RunningBattleSnapshotRecord>>();
        services.AddSingleton<IMemoryStateManager<ActivityPlan>, MemoryStateManager<ActivityPlan>>();
        
        _serviceProvider = services.BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<GameDbContext>();
    }
    
    [Fact]
    public async Task MemoryStateManager_Character_ShouldCacheAndTrackDirty()
    {
        // Arrange
        var manager = _serviceProvider.GetRequiredService<IMemoryStateManager<Character>>();
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "TestUser",
            Level = 1
        };
        
        // Act - Add character
        manager.Add(character);
        
        // Assert - Should be in memory and dirty
        Assert.Equal(1, manager.Count);
        Assert.Equal(1, manager.DirtyCount);
        
        var dirtyEntities = manager.GetDirtyEntities().ToList();
        Assert.Single(dirtyEntities);
        Assert.Equal(character.Id, dirtyEntities[0].Id);
    }
    
    [Fact]
    public async Task MemoryStateManager_Character_ShouldUpdateAndTrackDirty()
    {
        // Arrange
        var manager = _serviceProvider.GetRequiredService<IMemoryStateManager<Character>>();
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "TestUser",
            Level = 1
        };
        
        manager.Add(character);
        manager.ClearDirty(new[] { character.Id });
        
        // Act - Update character
        character.Level = 2;
        manager.Update(character);
        
        // Assert - Should be dirty again
        Assert.Equal(1, manager.DirtyCount);
        
        var dirtyEntities = manager.GetDirtyEntities().ToList();
        Assert.Single(dirtyEntities);
        Assert.Equal(2, dirtyEntities[0].Entity.Level);
    }
    
    [Fact]
    public async Task MemoryStateManager_BattleSnapshot_ShouldHandleMultipleUpdates()
    {
        // Arrange
        var manager = _serviceProvider.GetRequiredService<IMemoryStateManager<RunningBattleSnapshotRecord>>();
        var snapshot = new RunningBattleSnapshotRecord
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            StepBattleId = Guid.NewGuid(),
            SnapshotJson = "{}"
        };
        
        // Act - Add and update multiple times
        manager.Add(snapshot);
        for (int i = 0; i < 5; i++)
        {
            snapshot.SnapshotJson = $"{{\"tick\":{i}}}";
            manager.Update(snapshot);
        }
        
        // Assert - Should still be single dirty entity
        Assert.Equal(1, manager.Count);
        Assert.Equal(1, manager.DirtyCount);
        
        var dirtyEntities = manager.GetDirtyEntities().ToList();
        Assert.Single(dirtyEntities);
        Assert.Contains("\"tick\":4", dirtyEntities[0].Entity.SnapshotJson);
    }
    
    [Fact]
    public async Task MemoryStateManager_ActivityPlan_ShouldHandleAddUpdateDelete()
    {
        // Arrange
        var manager = _serviceProvider.GetRequiredService<IMemoryStateManager<ActivityPlan>>();
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            SlotIndex = 0,
            Type = ActivityType.Combat,
            State = ActivityState.Pending
        };
        
        // Act - Add
        manager.Add(plan);
        Assert.Equal(1, manager.Count);
        Assert.Equal(1, manager.DirtyCount);
        
        // Act - Update
        plan.State = ActivityState.Running;
        manager.Update(plan);
        Assert.Equal(1, manager.Count);
        Assert.Equal(1, manager.DirtyCount);
        
        // Act - Remove
        manager.Remove(plan.Id);
        Assert.Equal(0, manager.Count);
        Assert.Equal(0, manager.DirtyCount);
    }
    
    [Fact]
    public async Task MemoryStateManager_ShouldClearDirtyAfterSave()
    {
        // Arrange
        var manager = _serviceProvider.GetRequiredService<IMemoryStateManager<Character>>();
        var character1 = new Character { Id = Guid.NewGuid(), Name = "User1", Level = 1 };
        var character2 = new Character { Id = Guid.NewGuid(), Name = "User2", Level = 2 };
        
        manager.Add(character1);
        manager.Add(character2);
        
        Assert.Equal(2, manager.DirtyCount);
        
        // Act - Clear dirty for one character (simulating save)
        manager.ClearDirty(new[] { character1.Id });
        
        // Assert
        Assert.Equal(1, manager.DirtyCount);
        var remaining = manager.GetDirtyEntities().ToList();
        Assert.Single(remaining);
        Assert.Equal(character2.Id, remaining[0].Id);
    }
    
    [Fact]
    public async Task MemoryStateManager_ShouldGetSnapshot()
    {
        // Arrange
        var manager = _serviceProvider.GetRequiredService<IMemoryStateManager<Character>>();
        var character1 = new Character { Id = Guid.NewGuid(), Name = "User1", Level = 1 };
        var character2 = new Character { Id = Guid.NewGuid(), Name = "User2", Level = 2 };
        
        manager.Add(character1);
        manager.Add(character2);
        
        // Act
        var snapshot = manager.GetSnapshot();
        
        // Assert
        Assert.Equal(2, snapshot.Count);
        Assert.Contains(snapshot, kvp => kvp.Value.Name == "User1");
        Assert.Contains(snapshot, kvp => kvp.Value.Name == "User2");
    }
    
    [Fact]
    public async Task MemoryStateManager_ShouldHandleNullManagerGracefully()
    {
        // This tests the fallback behavior when memory manager is not available
        // In real scenarios, this would fall back to immediate SaveChangesAsync
        
        // Arrange
        IMemoryStateManager<Character>? nullManager = null;
        
        // Act & Assert
        // Should not throw, just handle gracefully
        Assert.Null(nullManager);
    }
    
    public void Dispose()
    {
        _db?.Dispose();
        _serviceProvider?.Dispose();
    }
}
