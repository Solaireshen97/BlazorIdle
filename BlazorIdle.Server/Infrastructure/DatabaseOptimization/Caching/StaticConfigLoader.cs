using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Abstractions;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Models;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching;

/// <summary>
/// 静态配置加载器接口
/// Static configuration loader interface
/// </summary>
public interface IStaticConfigLoader
{
    /// <summary>
    /// 启动时加载所有静态配置
    /// Load all static configurations on startup
    /// </summary>
    Task LoadAllConfigsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// 重新加载指定类型的配置
    /// Reload specific type of configuration
    /// </summary>
    /// <param name="configType">配置类型名称</param>
    /// <param name="ct">取消令牌</param>
    Task ReloadConfigAsync(string configType, CancellationToken ct = default);
    
    /// <summary>
    /// 获取指定配置
    /// Get specific configuration
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="key">配置键</param>
    /// <returns>配置对象或null</returns>
    T? GetConfig<T>(string key) where T : class;
    
    /// <summary>
    /// 获取所有指定类型的配置
    /// Get all configurations of specific type
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <returns>配置字典</returns>
    IReadOnlyDictionary<string, T> GetAllConfigs<T>() where T : class;
}

/// <summary>
/// 静态配置加载器实现
/// Static configuration loader implementation
/// 在应用启动时将静态配置数据加载到内存缓存
/// </summary>
public class StaticConfigLoader : IStaticConfigLoader, IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMultiTierCacheManager _cacheManager;
    private readonly ReadCacheOptions _options;
    private readonly ILogger<StaticConfigLoader> _logger;
    
    public StaticConfigLoader(
        IServiceScopeFactory scopeFactory,
        IMultiTierCacheManager cacheManager,
        IOptions<ReadCacheOptions> options,
        ILogger<StaticConfigLoader> logger)
    {
        _scopeFactory = scopeFactory;
        _cacheManager = cacheManager;
        _options = options.Value;
        _logger = logger;
    }
    
    /// <summary>
    /// 启动服务（IHostedService）
    /// </summary>
    public async Task StartAsync(CancellationToken ct)
    {
        if (!_options.StaticCache.Enabled || 
            !_options.StaticCache.LoadOnStartup)
        {
            _logger.LogInformation("静态配置加载已禁用，跳过");
            return;
        }
        
        _logger.LogInformation("开始加载静态配置到内存...");
        
        try
        {
            await LoadAllConfigsAsync(ct);
            _logger.LogInformation("静态配置加载完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "静态配置加载失败");
            // 不抛出异常，允许应用继续启动
        }
    }
    
    /// <summary>
    /// 停止服务（IHostedService）
    /// </summary>
    public Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("静态配置加载器停止");
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// 加载所有静态配置
    /// </summary>
    public async Task LoadAllConfigsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        // 加载装备定义（GearDefinition）
        // 注意：这里假设 GearDefinition 等实体类已存在
        // 实际实现时需要根据项目的实际实体类型调整
        
        // 由于当前代码中可能还没有完整的配置实体，这里提供一个通用的加载框架
        // 具体的实体类型可以根据项目需要添加
        
        var configTypes = new List<string>();
        
        // 从配置中获取预加载列表
        foreach (var configType in _options.Performance.PreloadOnStartup)
        {
            configTypes.Add(configType);
        }
        
        // 如果没有配置，使用默认列表
        if (configTypes.Count == 0)
        {
            configTypes.AddRange(new[] { "GearDefinition", "Affix", "GearSet" });
        }
        
        foreach (var configType in configTypes)
        {
            try
            {
                await LoadConfigTypeAsync(db, configType, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载配置类型失败: {ConfigType}", configType);
            }
        }
    }
    
    /// <summary>
    /// 重新加载指定配置类型
    /// </summary>
    public async Task ReloadConfigAsync(string configType, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        // 先清除旧缓存
        await _cacheManager.InvalidateByPatternAsync($"{configType}:*");
        
        // 重新加载
        await LoadConfigTypeAsync(db, configType, ct);
        
        _logger.LogInformation("配置类型重新加载完成: {ConfigType}", configType);
    }
    
    /// <summary>
    /// 获取指定配置
    /// </summary>
    public T? GetConfig<T>(string key) where T : class
    {
        var configType = typeof(T).Name;
        var cacheKey = $"{configType}:{key}";
        
        return _cacheManager.GetAsync<T>(cacheKey).GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// 获取所有指定类型的配置
    /// </summary>
    public IReadOnlyDictionary<string, T> GetAllConfigs<T>() where T : class
    {
        // 注意：由于我们使用 Static Cache（ConcurrentDictionary），
        // 理论上可以枚举所有键，但这里为了性能和封装性，
        // 建议在实际使用时通过其他方式获取所有配置ID列表
        
        // 这里返回空字典，实际实现时可以根据需要调整
        _logger.LogWarning("GetAllConfigs 方法需要根据具体实现调整");
        return new Dictionary<string, T>();
    }
    
    /// <summary>
    /// 加载指定类型的配置
    /// </summary>
    private async Task LoadConfigTypeAsync(GameDbContext db, string configType, CancellationToken ct)
    {
        // 根据配置类型加载对应的数据
        // 注意：这里需要根据实际项目的实体类型进行调整
        
        // 示例实现（伪代码）：
        // switch (configType)
        // {
        //     case "GearDefinition":
        //         var gearDefinitions = await db.GearDefinitions.ToListAsync(ct);
        //         foreach (var item in gearDefinitions)
        //         {
        //             await _cacheManager.SetAsync($"GearDefinition:{item.Id}", item, CacheTier.Static);
        //         }
        //         break;
        //     // ... 其他类型
        // }
        
        _logger.LogInformation("配置类型 {ConfigType} 的加载器需要具体实现", configType);
        
        // 为了让代码能编译通过，这里提供一个占位实现
        await Task.CompletedTask;
    }
}
