using System.Text.Json;
using BlazorIdle.Server.Domain.Shop.Configuration;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.Configuration;

/// <summary>
/// 商店配置选项
/// </summary>
public class ShopOptions
{
    // 缓存配置
    public bool EnableCaching { get; set; } = true;
    public int ShopDefinitionCacheMinutes { get; set; } = 60;
    public int ShopItemsCacheMinutes { get; set; } = 30;
    
    // 文件路径配置
    public string ConfigPath { get; set; } = "Config/Shop";
    public string ShopDefinitionsFile { get; set; } = "ShopDefinitions.json";
    public string ShopItemsFile { get; set; } = "ShopItems.json";
    
    // 商店配置
    public int DefaultRefreshIntervalSeconds { get; set; } = 3600;
    public int MaxShopNameLength { get; set; } = 50;
    public int MaxShopDescriptionLength { get; set; } = 200;
    
    // 商品配置
    public int MaxItemNameLength { get; set; } = 100;
    public int MaxItemDescriptionLength { get; set; } = 500;
    public int UnlimitedStock { get; set; } = -1;
    
    // 购买限制配置
    public int DailyResetSeconds { get; set; } = 86400;
    public int WeeklyResetSeconds { get; set; } = 604800;
    public int DefaultDailyLimit { get; set; } = 10;
    public int DefaultWeeklyLimit { get; set; } = 5;
    
    // 价格配置
    public int MinPriceAmount { get; set; } = 1;
    public int MaxPriceAmount { get; set; } = 1000000;
    
    // 验证配置
    public int MinLevelRequirement { get; set; } = 1;
    public int MaxLevelRequirement { get; set; } = 100;
    public int MinPurchaseQuantity { get; set; } = 1;
    public int MaxPurchaseQuantity { get; set; } = 999;
    
    // 查询配置
    public int DefaultPageSize { get; set; } = 20;
    public int MaxPageSize { get; set; } = 100;
    public int PurchaseHistoryDefaultDays { get; set; } = 30;
}

/// <summary>
/// 商店配置加载器接口
/// </summary>
public interface IShopConfigurationLoader
{
    /// <summary>
    /// 加载商店定义配置
    /// </summary>
    Task<ShopDefinitionsConfig> LoadShopDefinitionsAsync();
    
    /// <summary>
    /// 加载商品配置
    /// </summary>
    Task<ShopItemsConfig> LoadShopItemsAsync();
}

/// <summary>
/// 商店配置加载器实现
/// </summary>
public class ShopConfigurationLoader : IShopConfigurationLoader
{
    private readonly ShopOptions _options;
    private readonly ILogger<ShopConfigurationLoader> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IShopConfigurationValidator _validator;

    public ShopConfigurationLoader(
        IOptions<ShopOptions> options,
        ILogger<ShopConfigurationLoader> logger,
        IWebHostEnvironment env,
        IShopConfigurationValidator validator)
    {
        _options = options.Value;
        _logger = logger;
        _env = env;
        _validator = validator;
    }

    public async Task<ShopDefinitionsConfig> LoadShopDefinitionsAsync()
    {
        try
        {
            var filePath = Path.Combine(_env.ContentRootPath, _options.ConfigPath, _options.ShopDefinitionsFile);
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Shop definitions file not found at {FilePath}, returning empty config", filePath);
                return new ShopDefinitionsConfig();
            }

            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<ShopDefinitionsConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                _logger.LogWarning("Failed to deserialize shop definitions from {FilePath}", filePath);
                return new ShopDefinitionsConfig();
            }

            // 验证配置
            var (isValid, errors) = _validator.ValidateShopDefinitions(config);
            if (!isValid)
            {
                _logger.LogError("Shop definitions validation failed with {ErrorCount} errors", errors.Count);
                throw new InvalidOperationException($"商店定义配置验证失败: {string.Join("; ", errors)}");
            }

            _logger.LogInformation("Loaded and validated {Count} shop definitions from {FilePath}", 
                config.Shops.Count, filePath);
            
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading shop definitions configuration");
            return new ShopDefinitionsConfig();
        }
    }

    public async Task<ShopItemsConfig> LoadShopItemsAsync()
    {
        try
        {
            var filePath = Path.Combine(_env.ContentRootPath, _options.ConfigPath, _options.ShopItemsFile);
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Shop items file not found at {FilePath}, returning empty config", filePath);
                return new ShopItemsConfig();
            }

            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<ShopItemsConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                _logger.LogWarning("Failed to deserialize shop items from {FilePath}", filePath);
                return new ShopItemsConfig();
            }

            // 加载商店定义用于引用验证
            var shopDefinitions = await LoadShopDefinitionsAsync();
            
            // 验证配置
            var (isValid, errors) = _validator.ValidateShopItems(config, shopDefinitions);
            if (!isValid)
            {
                _logger.LogError("Shop items validation failed with {ErrorCount} errors", errors.Count);
                throw new InvalidOperationException($"商品配置验证失败: {string.Join("; ", errors)}");
            }

            _logger.LogInformation("Loaded and validated {Count} shop items from {FilePath}", 
                config.Items.Count, filePath);
            
            return config;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading shop items configuration");
            return new ShopItemsConfig();
        }
    }
}
