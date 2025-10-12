using BlazorIdle.Server.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorIdle.Tests.Shop;

/// <summary>
/// 商店配置验证测试
/// </summary>
public class ShopConfigurationValidationTests
{
    private readonly ILogger<ShopConfigurationLoader> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly ShopOptions _options;

    public ShopConfigurationValidationTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
            
        _logger = serviceProvider.GetRequiredService<ILogger<ShopConfigurationLoader>>();
        
        // 创建环境配置 - 使用当前测试输出目录
        _env = new TestWebHostEnvironment
        {
            ContentRootPath = Directory.GetCurrentDirectory()
        };
        
        _options = new ShopOptions
        {
            ConfigPath = "Config/Shop",
            ShopDefinitionsFile = "ShopDefinitions.json",
            ShopItemsFile = "ShopItems.json"
        };
    }

    [Fact]
    public async Task ValidateConfiguration_WithValidConfig_ShouldReturnNoErrors()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        var loader = new ShopConfigurationLoader(optionsWrapper, _logger, _env);

        // Act
        var errors = await loader.ValidateConfigurationAsync();

        // Assert
        // 注意：配置可能有警告但不应该有严重错误
        var criticalErrors = errors.Where(e => e.StartsWith("错误：")).ToList();
        Assert.Empty(criticalErrors);
    }

    [Fact]
    public async Task LoadShopDefinitions_ShouldLoadSuccessfully()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        var loader = new ShopConfigurationLoader(optionsWrapper, _logger, _env);

        // Act
        var config = await loader.LoadShopDefinitionsAsync();

        // Assert
        Assert.NotNull(config);
        Assert.NotEmpty(config.Shops);
        
        // 验证每个商店都有必要的字段
        foreach (var shop in config.Shops)
        {
            Assert.False(string.IsNullOrWhiteSpace(shop.Id), "商店ID不能为空");
            Assert.False(string.IsNullOrWhiteSpace(shop.Name), "商店名称不能为空");
        }
    }

    [Fact]
    public async Task LoadShopItems_ShouldLoadSuccessfully()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        var loader = new ShopConfigurationLoader(optionsWrapper, _logger, _env);

        // Act
        var config = await loader.LoadShopItemsAsync();

        // Assert
        Assert.NotNull(config);
        Assert.NotEmpty(config.Items);
        
        // 验证每个商品都有必要的字段
        foreach (var item in config.Items)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Id), "商品ID不能为空");
            Assert.False(string.IsNullOrWhiteSpace(item.ShopId), "商品所属商店ID不能为空");
            Assert.False(string.IsNullOrWhiteSpace(item.ItemName), "商品名称不能为空");
            Assert.True(item.Price.Amount > 0, "商品价格必须大于0");
        }
    }

    [Fact]
    public async Task ValidateConfiguration_ShouldDetectShopItemReferences()
    {
        // Arrange
        var optionsWrapper = Options.Create(_options);
        var loader = new ShopConfigurationLoader(optionsWrapper, _logger, _env);

        // Act
        var errors = await loader.ValidateConfigurationAsync();
        var shopsConfig = await loader.LoadShopDefinitionsAsync();
        var itemsConfig = await loader.LoadShopItemsAsync();

        // Assert
        var shopIds = shopsConfig.Shops.Select(s => s.Id).ToHashSet();
        
        // 确保所有商品引用的商店都存在
        foreach (var item in itemsConfig.Items)
        {
            Assert.Contains(item.ShopId, shopIds);
        }
        
        // 不应该有商店引用错误
        var referenceErrors = errors.Where(e => e.Contains("引用了不存在的商店")).ToList();
        Assert.Empty(referenceErrors);
    }
    
    /// <summary>
    /// 测试用 WebHost 环境
    /// </summary>
    private class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "BlazorIdle.Tests";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
