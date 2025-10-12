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
}
