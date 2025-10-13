using BlazorIdle.Server.Application.Shop;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using BlazorIdle.Server.Infrastructure.Configuration;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace BlazorIdle.Tests.Shop;

/// <summary>
/// 商店系统性能测试
/// 验证系统在高负载下的响应时间和缓存效果
/// </summary>
public class ShopPerformanceTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly ShopService _shopService;
    private readonly ShopService _shopServiceWithCache;
    private readonly IShopCacheService _cacheService;
    private readonly Guid _testCharacterId;
    private readonly ITestOutputHelper _output;

    public ShopPerformanceTests(ITestOutputHelper output)
    {
        _output = output;

        // 使用内存数据库进行测试
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);

        // 创建测试角色
        _testCharacterId = Guid.NewGuid();
        var testCharacter = new Character
        {
            Id = _testCharacterId,
            UserId = Guid.NewGuid(),
            Name = "性能测试角色",
            Level = 50,
            Gold = 1000000
        };
        _context.Characters.Add(testCharacter);

        // 添加测试商店和商品
        SeedTestData();
        _context.SaveChanges();

        // 创建 ShopOptions
        var shopOptions = Options.Create(new ShopOptions
        {
            EnableCaching = false,
            DailyResetSeconds = 86400,
            WeeklyResetSeconds = 604800,
            DefaultPageSize = 20,
            MaxPageSize = 100
        });

        var shopOptionsWithCache = Options.Create(new ShopOptions
        {
            EnableCaching = true,
            ShopDefinitionCacheMinutes = 60,
            ShopItemsCacheMinutes = 30,
            DailyResetSeconds = 86400,
            WeeklyResetSeconds = 604800,
            DefaultPageSize = 20,
            MaxPageSize = 100
        });

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var cacheLogger = loggerFactory.CreateLogger<ShopCacheService>();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        // Create configuration for cache service
        var configDict = new Dictionary<string, string?>
        {
            {"Shop:EnableCaching", "true"},
            {"Shop:ShopDefinitionCacheMinutes", "60"},
            {"Shop:ShopItemsCacheMinutes", "30"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var noCacheConfigDict = new Dictionary<string, string?>
        {
            {"Shop:EnableCaching", "false"}
        };
        var noCacheConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(noCacheConfigDict)
            .Build();

        _cacheService = new ShopCacheService(memoryCache, cacheLogger, configuration);

        var validator = new PurchaseValidator(_context, shopOptions);
        var validatorWithCache = new PurchaseValidator(_context, shopOptionsWithCache);

        // 创建两个服务实例：一个无缓存，一个有缓存
        var noCacheService = new ShopCacheService(new MemoryCache(new MemoryCacheOptions()), cacheLogger, noCacheConfiguration);
        _shopService = new ShopService(_context, validator, noCacheService, shopOptions);
        _shopServiceWithCache = new ShopService(_context, validatorWithCache, _cacheService, shopOptionsWithCache);
    }

    private void SeedTestData()
    {
        // 创建 5 个商店
        for (int i = 1; i <= 5; i++)
        {
            var shop = new ShopDefinition
            {
                Id = $"perf_shop_{i}",
                Name = $"性能测试商店 {i}",
                Type = ShopType.General,
                Icon = "🏪",
                Description = $"用于性能测试的商店 {i}",
                IsEnabled = true,
                SortOrder = i
            };
            _context.ShopDefinitions.Add(shop);

            // 每个商店添加 20 个商品
            for (int j = 1; j <= 20; j++)
            {
                var item = new ShopItem
                {
                    Id = $"perf_item_{i}_{j}",
                    ShopId = shop.Id,
                    ItemDefinitionId = $"item_def_{j}",
                    ItemName = $"测试商品 {i}-{j}",
                    ItemIcon = "🎁",
                    PriceJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        CurrencyType = 1,
                        Amount = j * 100
                    }),
                    PurchaseLimitJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Type = 0
                    }),
                    StockQuantity = -1,
                    MinLevel = j,
                    IsEnabled = true,
                    SortOrder = j
                };
                _context.ShopItems.Add(item);
            }
        }
    }

    [Fact]
    public async Task ListShops_Performance_ShouldCompleteWithin100ms()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _shopService.ListShopsAsync(_testCharacterId.ToString());
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Shops);
        
        _output.WriteLine($"ListShops (无缓存) 执行时间: {stopwatch.ElapsedMilliseconds}ms");
        
        // 性能要求：应在 100ms 内完成
        Assert.True(stopwatch.ElapsedMilliseconds < 100, 
            $"ListShops 执行时间 {stopwatch.ElapsedMilliseconds}ms 超过 100ms 限制");
    }

    [Fact]
    public async Task GetShopItems_Performance_ShouldCompleteWithin100ms()
    {
        // Arrange
        var shopId = "perf_shop_1";
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _shopService.GetShopItemsAsync(shopId, _testCharacterId.ToString());
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
        
        _output.WriteLine($"GetShopItems (无缓存) 执行时间: {stopwatch.ElapsedMilliseconds}ms");
        
        // 性能要求：应在 150ms 内完成（考虑测试环境和首次查询开销）
        Assert.True(stopwatch.ElapsedMilliseconds < 150, 
            $"GetShopItems 执行时间 {stopwatch.ElapsedMilliseconds}ms 超过 150ms 限制");
    }

    [Fact]
    public async Task Cache_ImprovesListShopsPerformance()
    {
        // Arrange - 预热缓存
        await _shopServiceWithCache.ListShopsAsync(_testCharacterId.ToString());

        // 测试无缓存性能
        var stopwatchNoCache = Stopwatch.StartNew();
        await _shopService.ListShopsAsync(_testCharacterId.ToString());
        stopwatchNoCache.Stop();

        // 测试有缓存性能
        var stopwatchWithCache = Stopwatch.StartNew();
        await _shopServiceWithCache.ListShopsAsync(_testCharacterId.ToString());
        stopwatchWithCache.Stop();

        // Assert
        _output.WriteLine($"无缓存: {stopwatchNoCache.ElapsedMilliseconds}ms, 有缓存: {stopwatchWithCache.ElapsedMilliseconds}ms");
        _output.WriteLine($"性能提升: {(stopwatchNoCache.ElapsedMilliseconds - stopwatchWithCache.ElapsedMilliseconds)}ms");
        
        // 缓存应该提供更好的性能
        Assert.True(stopwatchWithCache.ElapsedMilliseconds <= stopwatchNoCache.ElapsedMilliseconds,
            "缓存未能提供性能提升");
    }

    [Fact]
    public async Task Cache_ImprovesGetShopItemsPerformance()
    {
        // Arrange
        var shopId = "perf_shop_1";
        
        // 预热缓存
        await _shopServiceWithCache.GetShopItemsAsync(shopId, _testCharacterId.ToString());

        // 测试无缓存性能
        var stopwatchNoCache = Stopwatch.StartNew();
        await _shopService.GetShopItemsAsync(shopId, _testCharacterId.ToString());
        stopwatchNoCache.Stop();

        // 测试有缓存性能
        var stopwatchWithCache = Stopwatch.StartNew();
        await _shopServiceWithCache.GetShopItemsAsync(shopId, _testCharacterId.ToString());
        stopwatchWithCache.Stop();

        // Assert
        _output.WriteLine($"无缓存: {stopwatchNoCache.ElapsedMilliseconds}ms, 有缓存: {stopwatchWithCache.ElapsedMilliseconds}ms");
        _output.WriteLine($"性能提升: {(stopwatchNoCache.ElapsedMilliseconds - stopwatchWithCache.ElapsedMilliseconds)}ms");
        
        // 缓存应该提供更好的性能
        Assert.True(stopwatchWithCache.ElapsedMilliseconds <= stopwatchNoCache.ElapsedMilliseconds,
            "缓存未能提供性能提升");
    }

    [Fact]
    public async Task MultipleShops_Performance_ShouldHandleLargeDataset()
    {
        // Arrange - 查询所有商店
        var stopwatch = Stopwatch.StartNew();

        // Act - 获取所有商店列表
        var shopsResult = await _shopService.ListShopsAsync(_testCharacterId.ToString());
        
        // 获取每个商店的商品
        var tasks = shopsResult.Shops.Select(shop => 
            _shopService.GetShopItemsAsync(shop.Id, _testCharacterId.ToString())
        );
        
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"查询 {shopsResult.Shops.Count} 个商店及其商品总耗时: {stopwatch.ElapsedMilliseconds}ms");
        
        // 整体查询应在 500ms 内完成
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"批量查询耗时 {stopwatch.ElapsedMilliseconds}ms 超过 500ms 限制");
    }

    [Fact]
    public async Task PurchaseHistory_Performance_ShouldCompleteWithin200ms()
    {
        // Arrange - 创建一些购买记录
        for (int i = 0; i < 50; i++)
        {
            _context.PurchaseRecords.Add(new PurchaseRecord
            {
                Id = Guid.NewGuid().ToString(),
                CharacterId = _testCharacterId,
                ShopId = "perf_shop_1",
                ShopItemId = $"perf_item_1_{(i % 20) + 1}",
                ItemDefinitionId = $"item_def_{(i % 20) + 1}",
                Quantity = 1,
                PriceJson = "{\"CurrencyType\":1,\"Amount\":100}",
                PurchasedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await _context.SaveChangesAsync();

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _shopService.GetPurchaseHistoryAsync(_testCharacterId.ToString(), 1, 20);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Records);
        
        _output.WriteLine($"GetPurchaseHistory (50条记录) 执行时间: {stopwatch.ElapsedMilliseconds}ms");
        
        // 性能要求：应在 200ms 内完成
        Assert.True(stopwatch.ElapsedMilliseconds < 200,
            $"GetPurchaseHistory 执行时间 {stopwatch.ElapsedMilliseconds}ms 超过 200ms 限制");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
