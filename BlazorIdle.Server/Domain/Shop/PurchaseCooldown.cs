namespace BlazorIdle.Server.Domain.Shop;

/// <summary>
/// 购买冷却记录
/// 用于防止恶意刷购买请求
/// </summary>
public class PurchaseCooldown
{
    /// <summary>
    /// 冷却记录 ID
    /// 格式：{characterId}_global 或 {characterId}_{shopItemId}
    /// </summary>
    public string Id { get; set; } = null!;
    
    /// <summary>
    /// 角色 ID
    /// </summary>
    public string CharacterId { get; set; } = null!;
    
    /// <summary>
    /// 商品 ID（null 表示全局冷却）
    /// </summary>
    public string? ShopItemId { get; set; }
    
    /// <summary>
    /// 冷却结束时间
    /// </summary>
    public DateTime CooldownUntil { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 生成冷却记录 ID
    /// </summary>
    public static string GenerateId(string characterId, string? shopItemId = null)
    {
        return shopItemId == null 
            ? $"{characterId}_global"
            : $"{characterId}_{shopItemId}";
    }
    
    /// <summary>
    /// 检查冷却是否已过期
    /// </summary>
    public bool IsExpired()
    {
        return DateTime.UtcNow >= CooldownUntil;
    }
    
    /// <summary>
    /// 获取剩余冷却时间（秒）
    /// </summary>
    public double GetRemainingSeconds()
    {
        if (IsExpired())
            return 0;
            
        return (CooldownUntil - DateTime.UtcNow).TotalSeconds;
    }
}
