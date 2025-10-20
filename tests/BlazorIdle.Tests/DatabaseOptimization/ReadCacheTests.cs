using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Abstractions;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BlazorIdle.Tests.DatabaseOptimization;

/// <summary>
/// MultiTierCacheManager 单元测试
/// Unit tests for MultiTierCacheManager
/// </summary>
public class ReadCacheTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<MultiTierCacheManager>> _loggerMock;
    private readonly ReadCacheOptions _options;
    private readonly IMultiTierCacheManager _cacheManager;
    
    public ReadCacheTests()
    {
        _memoryCache = new MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions
        {
            SizeLimit = 10000
        });
        
        _loggerMock = new Mock<ILogger<MultiTierCacheManager>>();
        
        _options = new ReadCacheOptions
        {
            EnableReadCache = true,
            MaxCacheSize = 100000,
            EnableStatistics = true,
            SessionCache = new SessionCacheOptions
            {
                Enabled = true,
                DefaultTtlMinutes = 30,
                MaxSize = 10000
            },
            EntityCache = new EntityCacheOptions
            {
                Enabled = true,
                DefaultTtlMinutes = 15,
                MaxSize = 50000,
                EvictionPolicy = "LRU"
            },
            StaticCache = new StaticCacheOptions
            {
                Enabled = true,
                LoadOnStartup = true,
                MaxSize = 50000
            },
            Performance = new PerformanceOptions
            {
                EnableAntiCrashing = true,
                AntiCrashingSemaphoreTimeout = 5000
            }
        };
        
        var optionsMock = Options.Create(_options);
        _cacheManager = new MultiTierCacheManager(_memoryCache, optionsMock, _loggerMock.Object);
    }
    
    public void Dispose()
    {
        _memoryCache.Dispose();
    }
    
    [Fact]
    public async Task GetOrLoadAsync_CacheMiss_ShouldLoadFromDatabase()
    {
        // Arrange
        var cacheKey = "CacheEntity:123";
        var expectedValue = new CacheEntity { Id = 123, Name = "Test" };
        var loaderCalled = false;
        
        Func<Task<CacheEntity?>> loader = async () =>
        {
            loaderCalled = true;
            await Task.Delay(10);
            return expectedValue;
        };
        
        // Act
        var result = await _cacheManager.GetOrLoadAsync(
            cacheKey,
            loader,
            CacheTier.Entity,
            TimeSpan.FromMinutes(15)
        );
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedValue.Id, result.Id);
        Assert.Equal(expectedValue.Name, result.Name);
        Assert.True(loaderCalled, "Loader should be called for cache miss");
    }
    
    [Fact]
    public async Task GetOrLoadAsync_CacheHit_ShouldReturnFromCache()
    {
        // Arrange
        var cacheKey = "CacheEntity:456";
        var expectedValue = new CacheEntity { Id = 456, Name = "Cached" };
        var loaderCallCount = 0;
        
        Func<Task<CacheEntity?>> loader = async () =>
        {
            loaderCallCount++;
            await Task.Delay(10);
            return expectedValue;
        };
        
        // Act - First call (cache miss)
        var result1 = await _cacheManager.GetOrLoadAsync(
            cacheKey,
            loader,
            CacheTier.Entity,
            TimeSpan.FromMinutes(15)
        );
        
        // Act - Second call (should hit cache)
        var result2 = await _cacheManager.GetOrLoadAsync(
            cacheKey,
            loader,
            CacheTier.Entity,
            TimeSpan.FromMinutes(15)
        );
        
        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(expectedValue.Id, result2.Id);
        Assert.Equal(1, loaderCallCount); // Loader should only be called once
    }
    
    [Fact]
    public async Task GetOrLoadAsync_ConcurrentRequests_ShouldOnlyLoadOnce()
    {
        // Arrange
        var cacheKey = "CacheEntity:789";
        var expectedValue = new CacheEntity { Id = 789, Name = "Concurrent" };
        var loaderCallCount = 0;
        
        Func<Task<CacheEntity?>> loader = async () =>
        {
            Interlocked.Increment(ref loaderCallCount);
            await Task.Delay(100); // Simulate slow DB query
            return expectedValue;
        };
        
        // Act - 10 concurrent requests for the same key
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _cacheManager.GetOrLoadAsync(
                cacheKey,
                loader,
                CacheTier.Entity,
                TimeSpan.FromMinutes(15)
            ))
            .ToArray();
        
        var results = await Task.WhenAll(tasks);
        
        // Assert
        Assert.All(results, r => Assert.NotNull(r));
        Assert.All(results, r => Assert.Equal(expectedValue.Id, r!.Id));
        Assert.Equal(1, loaderCallCount); // Anti-crash mechanism should ensure only one load
    }
    
    [Fact]
    public async Task InvalidateAsync_ShouldRemoveFromCache()
    {
        // Arrange
        var cacheKey = "CacheEntity:999";
        var value = new CacheEntity { Id = 999, Name = "ToInvalidate" };
        var loaderCallCount = 0;
        
        Func<Task<CacheEntity?>> loader = async () =>
        {
            loaderCallCount++;
            await Task.Delay(10);
            return value;
        };
        
        // Act - Load into cache
        await _cacheManager.GetOrLoadAsync(cacheKey, loader, CacheTier.Entity, TimeSpan.FromMinutes(15));
        
        // Act - Invalidate
        await _cacheManager.InvalidateAsync(cacheKey);
        
        // Act - Try to get again (should reload)
        await _cacheManager.GetOrLoadAsync(cacheKey, loader, CacheTier.Entity, TimeSpan.FromMinutes(15));
        
        // Assert
        Assert.Equal(2, loaderCallCount); // Should load twice (initial + after invalidation)
    }
    
    [Fact]
    public async Task GetStatistics_ShouldReturnCorrectMetrics()
    {
        // Arrange
        var cacheKey1 = "Stats:1";
        var cacheKey2 = "Stats:2";
        var value = new CacheEntity { Id = 1, Name = "Stats" };
        
        // Act - Generate some cache hits and misses
        await _cacheManager.GetOrLoadAsync(cacheKey1, async () => value, CacheTier.Entity, TimeSpan.FromMinutes(15)); // Miss
        await _cacheManager.GetOrLoadAsync(cacheKey1, async () => value, CacheTier.Entity, TimeSpan.FromMinutes(15)); // Hit
        await _cacheManager.GetOrLoadAsync(cacheKey2, async () => value, CacheTier.Entity, TimeSpan.FromMinutes(15)); // Miss
        await _cacheManager.GetOrLoadAsync(cacheKey2, async () => value, CacheTier.Entity, TimeSpan.FromMinutes(15)); // Hit
        
        // Act
        var stats = _cacheManager.GetStatistics();
        
        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.TotalOperations >= 4, $"Expected at least 4 operations, got {stats.TotalOperations}");
        Assert.True(stats.Hits >= 2, $"Expected at least 2 hits, got {stats.Hits}");
        Assert.True(stats.HitRate > 0, $"Expected positive hit rate, got {stats.HitRate}");
    }
    
    [Fact]
    public async Task DisabledCache_ShouldNotCache()
    {
        // Arrange - Create manager with disabled cache
        var disabledOptions = new ReadCacheOptions { EnableReadCache = false };
        var disabledManager = new MultiTierCacheManager(
            _memoryCache,
            Options.Create(disabledOptions),
            _loggerMock.Object
        );
        
        var loaderCallCount = 0;
        Func<Task<CacheEntity?>> loader = async () =>
        {
            loaderCallCount++;
            return new CacheEntity { Id = 1, Name = "Test" };
        };
        
        // Act - Call twice
        await disabledManager.GetOrLoadAsync("Test:1", loader, CacheTier.Entity, TimeSpan.FromMinutes(15));
        await disabledManager.GetOrLoadAsync("Test:1", loader, CacheTier.Entity, TimeSpan.FromMinutes(15));
        
        // Assert - Loader should be called twice (no caching)
        Assert.Equal(2, loaderCallCount);
    }
}

/// <summary>
/// 缓存测试实体类
/// Cache test entity class
/// </summary>
public class CacheEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
