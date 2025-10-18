using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Config.DatabaseOptimization;

/// <summary>
/// 缓存配置 - 定义不同实体类型的缓存策略
/// Cache Configuration - Define caching strategies for different entity types
/// </summary>
public class CacheConfiguration
{
    /// <summary>
    /// 实体特定的缓存策略
    /// Entity-specific cache strategies
    /// 
    /// Key: 实体类型名称（如 "GearDefinition", "Character"）
    /// Value: 该实体的缓存策略
    /// </summary>
    public Dictionary<string, EntityCacheStrategy> EntityStrategies { get; set; } 
        = new Dictionary<string, EntityCacheStrategy>();
    
    /// <summary>
    /// 全局缓存设置
    /// Global cache settings
    /// </summary>
    public GlobalCacheSettings GlobalSettings { get; set; } = new GlobalCacheSettings();
}
