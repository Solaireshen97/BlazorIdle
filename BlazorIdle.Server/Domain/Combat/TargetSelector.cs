using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Rng;
using System.Collections.Generic;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat;

/// <summary>
/// 目标选取管理器：基于仇恨权重的随机目标选择
/// Phase 2: 实现加权随机算法，支持玩家攻击随机选怪
/// </summary>
public class TargetSelector
{
    private readonly RngContext _rng;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="rng">随机数生成器，保证可重放性</param>
    public TargetSelector(RngContext rng)
    {
        _rng = rng;
    }
    
    /// <summary>
    /// 从目标池中随机选择一个目标
    /// </summary>
    /// <param name="candidates">候选目标列表</param>
    /// <returns>选中的目标，如果无可选目标返回 null</returns>
    public ICombatant? SelectTarget(IEnumerable<ICombatant> candidates)
    {
        var available = candidates.Where(c => c.CanBeTargeted()).ToList();
        if (available.Count == 0) return null;
        
        // 计算总权重
        double totalWeight = available.Sum(c => c.ThreatWeight);
        
        // 随机选择
        double roll = _rng.NextDouble() * totalWeight;
        double cumulative = 0;
        
        foreach (var candidate in available)
        {
            cumulative += candidate.ThreatWeight;
            if (roll <= cumulative)
                return candidate;
        }
        
        return available.Last(); // 保底
    }
}
