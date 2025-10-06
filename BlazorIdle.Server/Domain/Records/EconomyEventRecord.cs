using System;

namespace BlazorIdle.Server.Domain.Records;

/// <summary>
/// 经济事件记录，用于追踪奖励发放与幂等性检查。
/// </summary>
public class EconomyEventRecord
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public Guid? BattleId { get; set; }
    
    /// <summary>
    /// 事件类型：battle_segment_reward, battle_final_reward, etc.
    /// </summary>
    public string EventType { get; set; } = "";
    
    /// <summary>
    /// 幂等键：用于防止重复发放。格式如: "battle:{battleId}:segment:{segmentIndex}"
    /// </summary>
    public string IdempotencyKey { get; set; } = "";
    
    public long Gold { get; set; }
    public long Exp { get; set; }
    
    /// <summary>
    /// 物品奖励 JSON: {"itemId": quantity, ...}
    /// </summary>
    public string? ItemsJson { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
