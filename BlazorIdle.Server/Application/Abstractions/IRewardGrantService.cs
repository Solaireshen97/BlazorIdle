using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 奖励发放服务：负责将金币/经验/物品发放到角色账户，带幂等性检查。
/// </summary>
public interface IRewardGrantService
{
    /// <summary>
    /// 发放奖励（幂等）。
    /// </summary>
    /// <param name="characterId">角色 ID</param>
    /// <param name="gold">金币数量</param>
    /// <param name="exp">经验数量</param>
    /// <param name="items">物品字典: itemId -> quantity</param>
    /// <param name="idempotencyKey">幂等键：防止重复发放</param>
    /// <param name="eventType">事件类型标识，如 "battle_segment_reward"</param>
    /// <param name="battleId">关联的战斗 ID（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>是否实际发放（false 表示已发放过，被幂等性拦截）</returns>
    Task<bool> GrantRewardsAsync(
        Guid characterId,
        long gold,
        long exp,
        Dictionary<string, int> items,
        string idempotencyKey,
        string eventType,
        Guid? battleId = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// 检查是否已经发放过（幂等性检查）。
    /// </summary>
    Task<bool> IsAlreadyGrantedAsync(string idempotencyKey, CancellationToken ct = default);
}
