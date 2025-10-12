using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 装备属性缓存服务测试
/// </summary>
public class EquipmentStatsCacheServiceTests
{
    private readonly EquipmentStatsCacheService _cacheService;
    private readonly IMemoryCache _cache;

    public EquipmentStatsCacheServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _cacheService = new EquipmentStatsCacheService(_cache);
    }

    [Fact]
    public async Task GetOrCalculateAsync_ShouldCallCalculateFuncOnCacheMiss()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var callCount = 0;
        var expectedStats = new Dictionary<StatType, double>
        {
            { StatType.Health, 1000 },
            { StatType.AttackPower, 100 }
        };

        Task<Dictionary<StatType, double>> CalculateFunc()
        {
            callCount++;
            return Task.FromResult(expectedStats);
        }

        // Act
        var result = await _cacheService.GetOrCalculateAsync(characterId, CalculateFunc);

        // Assert
        Assert.Equal(1, callCount); // 计算函数应该被调用一次
        Assert.Equal(expectedStats, result);
    }

    [Fact]
    public async Task GetOrCalculateAsync_ShouldReturnCachedValueOnCacheHit()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var callCount = 0;
        var expectedStats = new Dictionary<StatType, double>
        {
            { StatType.Health, 1000 },
            { StatType.AttackPower, 100 }
        };

        Task<Dictionary<StatType, double>> CalculateFunc()
        {
            callCount++;
            return Task.FromResult(expectedStats);
        }

        // 第一次调用，填充缓存
        await _cacheService.GetOrCalculateAsync(characterId, CalculateFunc);

        // Act - 第二次调用，应该命中缓存
        var result = await _cacheService.GetOrCalculateAsync(characterId, CalculateFunc);

        // Assert
        Assert.Equal(1, callCount); // 计算函数应该只被调用一次（第二次命中缓存）
        Assert.Equal(expectedStats, result);
    }

    [Fact]
    public async Task Invalidate_ShouldClearCache()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var callCount = 0;
        var expectedStats = new Dictionary<StatType, double>
        {
            { StatType.Health, 1000 }
        };

        Task<Dictionary<StatType, double>> CalculateFunc()
        {
            callCount++;
            return Task.FromResult(expectedStats);
        }

        // 第一次调用，填充缓存
        await _cacheService.GetOrCalculateAsync(characterId, CalculateFunc);

        // Act - 使缓存失效
        _cacheService.Invalidate(characterId);

        // 再次调用，应该重新计算
        await _cacheService.GetOrCalculateAsync(characterId, CalculateFunc);

        // Assert
        Assert.Equal(2, callCount); // 计算函数应该被调用两次（第二次缓存已失效）
    }

    [Fact]
    public async Task GetOrCalculateAsync_DifferentCharacters_ShouldHaveSeparateCaches()
    {
        // Arrange
        var characterId1 = Guid.NewGuid();
        var characterId2 = Guid.NewGuid();
        var callCount = 0;

        Task<Dictionary<StatType, double>> CalculateFunc()
        {
            callCount++;
            return Task.FromResult(new Dictionary<StatType, double>
            {
                { StatType.Health, 1000 }
            });
        }

        // Act
        await _cacheService.GetOrCalculateAsync(characterId1, CalculateFunc);
        await _cacheService.GetOrCalculateAsync(characterId2, CalculateFunc);

        // Assert
        Assert.Equal(2, callCount); // 不同角色应该各自计算
    }

    [Fact]
    public async Task InvalidateBatch_ShouldInvalidateMultipleCharacters()
    {
        // Arrange
        var character1 = Guid.NewGuid();
        var character2 = Guid.NewGuid();
        var character3 = Guid.NewGuid();

        var stats = new Dictionary<StatType, double> { { StatType.Health, 1000 } };

        // 填充缓存
        await _cacheService.GetOrCalculateAsync(character1, () => Task.FromResult(stats));
        await _cacheService.GetOrCalculateAsync(character2, () => Task.FromResult(stats));
        await _cacheService.GetOrCalculateAsync(character3, () => Task.FromResult(stats));

        // Act
        _cacheService.InvalidateBatch(new[] { character1, character2 });

        // Assert
        var callCount = 0;
        Task<Dictionary<StatType, double>> CalculateFunc()
        {
            callCount++;
            return Task.FromResult(stats);
        }

        // character1 和 character2 缓存已失效，应该重新计算
        await _cacheService.GetOrCalculateAsync(character1, CalculateFunc);
        await _cacheService.GetOrCalculateAsync(character2, CalculateFunc);
        Assert.Equal(2, callCount);

        // character3 缓存仍然有效，不应重新计算
        await _cacheService.GetOrCalculateAsync(character3, CalculateFunc);
        Assert.Equal(2, callCount); // 不变
    }

    [Fact]
    public async Task GetOrCalculateAsync_ShouldHandleEmptyStats()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var emptyStats = new Dictionary<StatType, double>();

        // Act
        var result = await _cacheService.GetOrCalculateAsync(
            characterId,
            () => Task.FromResult(emptyStats)
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOrCalculateAsync_ShouldPreserveStatsValues()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var expectedStats = new Dictionary<StatType, double>
        {
            { StatType.Health, 1234.56 },
            { StatType.AttackPower, 789.01 },
            { StatType.CritChance, 0.25 },
            { StatType.Armor, 500.0 }
        };

        // Act
        var result = await _cacheService.GetOrCalculateAsync(
            characterId,
            () => Task.FromResult(expectedStats)
        );

        // Assert
        Assert.Equal(expectedStats.Count, result.Count);
        foreach (var kvp in expectedStats)
        {
            Assert.True(result.ContainsKey(kvp.Key));
            Assert.Equal(kvp.Value, result[kvp.Key]);
        }
    }
}
