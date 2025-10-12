using System.Text.Json;
using BlazorIdle.Server.Domain.Shop.Configuration;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.Configuration;

/// <summary>
/// 商店配置选项
/// </summary>
public class ShopOptions
{
    public bool EnableCaching { get; set; } = true;
    public int ShopDefinitionCacheMinutes { get; set; } = 60;
    public int ShopItemsCacheMinutes { get; set; } = 30;
    public string ConfigPath { get; set; } = "Config/Shop";
    public string ShopDefinitionsFile { get; set; } = "ShopDefinitions.json";
    public string ShopItemsFile { get; set; } = "ShopItems.json";
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
    
    /// <summary>
    /// 验证配置完整性
    /// </summary>
    /// <returns>验证错误列表，如果为空则表示配置有效</returns>
    Task<List<string>> ValidateConfigurationAsync();
}

/// <summary>
/// 商店配置加载器实现
/// </summary>
public class ShopConfigurationLoader : IShopConfigurationLoader
{
    private readonly ShopOptions _options;
    private readonly ILogger<ShopConfigurationLoader> _logger;
    private readonly IWebHostEnvironment _env;

    public ShopConfigurationLoader(
        IOptions<ShopOptions> options,
        ILogger<ShopConfigurationLoader> logger,
        IWebHostEnvironment env)
    {
        _options = options.Value;
        _logger = logger;
        _env = env;
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

            _logger.LogInformation("Loaded {Count} shop definitions from {FilePath}", 
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

            _logger.LogInformation("Loaded {Count} shop items from {FilePath}", 
                config.Items.Count, filePath);
            
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading shop items configuration");
            return new ShopItemsConfig();
        }
    }

    /// <summary>
    /// 验证配置完整性
    /// </summary>
    public async Task<List<string>> ValidateConfigurationAsync()
    {
        var errors = new List<string>();

        try
        {
            // 加载配置
            var shopsConfig = await LoadShopDefinitionsAsync();
            var itemsConfig = await LoadShopItemsAsync();

            // 验证商店定义
            if (shopsConfig.Shops.Count == 0)
            {
                errors.Add("警告：没有配置任何商店");
            }

            var shopIds = new HashSet<string>();
            foreach (var shop in shopsConfig.Shops)
            {
                // 检查商店ID重复
                if (!shopIds.Add(shop.Id))
                {
                    errors.Add($"错误：商店ID重复 - {shop.Id}");
                }

                // 检查必填字段
                if (string.IsNullOrWhiteSpace(shop.Name))
                {
                    errors.Add($"错误：商店 {shop.Id} 缺少名称");
                }

                // 检查名称长度
                if (shop.Name.Length > ShopSystemConfig.ShopConfig.MaxShopNameLength)
                {
                    errors.Add($"警告：商店 {shop.Id} 名称过长（{shop.Name.Length} > {ShopSystemConfig.ShopConfig.MaxShopNameLength}）");
                }
            }

            // 验证商品配置
            if (itemsConfig.Items.Count == 0)
            {
                errors.Add("警告：没有配置任何商品");
            }

            var itemIds = new HashSet<string>();
            foreach (var item in itemsConfig.Items)
            {
                // 检查商品ID重复
                if (!itemIds.Add(item.Id))
                {
                    errors.Add($"错误：商品ID重复 - {item.Id}");
                }

                // 检查必填字段
                if (string.IsNullOrWhiteSpace(item.ItemName))
                {
                    errors.Add($"错误：商品 {item.Id} 缺少名称");
                }

                // 检查商店引用
                if (!shopIds.Contains(item.ShopId))
                {
                    errors.Add($"错误：商品 {item.Id} 引用了不存在的商店 {item.ShopId}");
                }

                // 检查价格有效性
                if (item.Price.Amount < ShopSystemConfig.PriceConfig.MinPriceAmount)
                {
                    errors.Add($"错误：商品 {item.Id} 价格低于最小值（{item.Price.Amount} < {ShopSystemConfig.PriceConfig.MinPriceAmount}）");
                }

                if (item.Price.Amount > ShopSystemConfig.PriceConfig.MaxPriceAmount)
                {
                    errors.Add($"警告：商品 {item.Id} 价格过高（{item.Price.Amount} > {ShopSystemConfig.PriceConfig.MaxPriceAmount}）");
                }

                // 检查等级要求
                if (item.MinLevel < ShopSystemConfig.ValidationConfig.MinLevelRequirement ||
                    item.MinLevel > ShopSystemConfig.ValidationConfig.MaxLevelRequirement)
                {
                    errors.Add($"警告：商品 {item.Id} 等级要求超出范围（{item.MinLevel}）");
                }
            }

            // 记录验证结果
            if (errors.Count == 0)
            {
                _logger.LogInformation("商店配置验证通过：{ShopCount} 个商店，{ItemCount} 个商品", 
                    shopsConfig.Shops.Count, itemsConfig.Items.Count);
            }
            else
            {
                _logger.LogWarning("商店配置验证发现 {ErrorCount} 个问题", errors.Count);
                foreach (var error in errors)
                {
                    _logger.LogWarning("配置问题：{Error}", error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "配置验证过程中发生错误");
            errors.Add($"验证失败：{ex.Message}");
        }

        return errors;
    }
}
