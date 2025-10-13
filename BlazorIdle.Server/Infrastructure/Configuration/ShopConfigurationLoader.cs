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

    /// <summary>
    /// 验证配置值的有效性
    /// </summary>
    public void Validate()
    {
        if (ShopDefinitionCacheMinutes < 1)
            throw new InvalidOperationException("ShopDefinitionCacheMinutes must be at least 1 minute");
        
        if (ShopItemsCacheMinutes < 1)
            throw new InvalidOperationException("ShopItemsCacheMinutes must be at least 1 minute");
        
        if (string.IsNullOrWhiteSpace(ConfigPath))
            throw new InvalidOperationException("ConfigPath cannot be empty");
        
        if (string.IsNullOrWhiteSpace(ShopDefinitionsFile))
            throw new InvalidOperationException("ShopDefinitionsFile cannot be empty");
        
        if (string.IsNullOrWhiteSpace(ShopItemsFile))
            throw new InvalidOperationException("ShopItemsFile cannot be empty");
        
        if (DefaultRefreshIntervalSeconds < 60)
            throw new InvalidOperationException("DefaultRefreshIntervalSeconds must be at least 60 seconds");
        
        if (MaxShopNameLength < 1 || MaxShopNameLength > 1000)
            throw new InvalidOperationException("MaxShopNameLength must be between 1 and 1000");
        
        if (MaxItemNameLength < 1 || MaxItemNameLength > 1000)
            throw new InvalidOperationException("MaxItemNameLength must be between 1 and 1000");
        
        if (DailyResetSeconds < 3600)
            throw new InvalidOperationException("DailyResetSeconds must be at least 3600 (1 hour)");
        
        if (WeeklyResetSeconds < 86400)
            throw new InvalidOperationException("WeeklyResetSeconds must be at least 86400 (1 day)");
        
        if (MinPriceAmount < 0)
            throw new InvalidOperationException("MinPriceAmount cannot be negative");
        
        if (MaxPriceAmount < MinPriceAmount)
            throw new InvalidOperationException("MaxPriceAmount must be greater than MinPriceAmount");
        
        if (MinLevelRequirement < 1)
            throw new InvalidOperationException("MinLevelRequirement must be at least 1");
        
        if (MaxLevelRequirement < MinLevelRequirement)
            throw new InvalidOperationException("MaxLevelRequirement must be greater than MinLevelRequirement");
        
        if (MinPurchaseQuantity < 1)
            throw new InvalidOperationException("MinPurchaseQuantity must be at least 1");
        
        if (MaxPurchaseQuantity < MinPurchaseQuantity)
            throw new InvalidOperationException("MaxPurchaseQuantity must be greater than MinPurchaseQuantity");
        
        if (DefaultPageSize < 1 || DefaultPageSize > MaxPageSize)
            throw new InvalidOperationException("DefaultPageSize must be between 1 and MaxPageSize");
        
        if (MaxPageSize < 1 || MaxPageSize > 1000)
            throw new InvalidOperationException("MaxPageSize must be between 1 and 1000");
        
        if (PurchaseHistoryDefaultDays < 1)
            throw new InvalidOperationException("PurchaseHistoryDefaultDays must be at least 1");
    }
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
        
        // Validate configuration on startup
        try
        {
            _options.Validate();
            _logger.LogInformation("Shop configuration validated successfully");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Shop configuration validation failed: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<ShopDefinitionsConfig> LoadShopDefinitionsAsync()
    {
        try
        {
            var filePath = Path.Combine(_env.ContentRootPath, _options.ConfigPath, _options.ShopDefinitionsFile);
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Shop definitions file not found at {FilePath}, returning empty config. " +
                    "Ensure the file exists in the Config/Shop directory.", filePath);
                return new ShopDefinitionsConfig();
            }

            var json = await File.ReadAllTextAsync(filePath);
            
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Shop definitions file at {FilePath} is empty, returning empty config", filePath);
                return new ShopDefinitionsConfig();
            }

            var config = JsonSerializer.Deserialize<ShopDefinitionsConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
            });

            if (config == null)
            {
                _logger.LogWarning("Failed to deserialize shop definitions from {FilePath}. " +
                    "Check JSON format and ensure it matches ShopDefinitionsConfig structure.", filePath);
                return new ShopDefinitionsConfig();
            }

            _logger.LogInformation("Successfully loaded {Count} shop definitions from {FilePath}", 
                config.Shops?.Count ?? 0, filePath);
            
            return config;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "JSON parsing error in shop definitions file. " +
                "Check for syntax errors at line {LineNumber}, position {BytePosition}", 
                jsonEx.LineNumber, jsonEx.BytePositionInLine);
            return new ShopDefinitionsConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading shop definitions configuration from {FilePath}", 
                Path.Combine(_env.ContentRootPath, _options.ConfigPath, _options.ShopDefinitionsFile));
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
                _logger.LogWarning("Shop items file not found at {FilePath}, returning empty config. " +
                    "Ensure the file exists in the Config/Shop directory.", filePath);
                return new ShopItemsConfig();
            }

            var json = await File.ReadAllTextAsync(filePath);
            
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Shop items file at {FilePath} is empty, returning empty config", filePath);
                return new ShopItemsConfig();
            }

            var config = JsonSerializer.Deserialize<ShopItemsConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
            });

            if (config == null)
            {
                _logger.LogWarning("Failed to deserialize shop items from {FilePath}. " +
                    "Check JSON format and ensure it matches ShopItemsConfig structure.", filePath);
                return new ShopItemsConfig();
            }

            _logger.LogInformation("Successfully loaded {Count} shop items from {FilePath}", 
                config.Items?.Count ?? 0, filePath);
            
            return config;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "JSON parsing error in shop items file. " +
                "Check for syntax errors at line {LineNumber}, position {BytePosition}", 
                jsonEx.LineNumber, jsonEx.BytePositionInLine);
            return new ShopItemsConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading shop items configuration from {FilePath}", 
                Path.Combine(_env.ContentRootPath, _options.ConfigPath, _options.ShopItemsFile));
            return new ShopItemsConfig();
        }
    }
}
