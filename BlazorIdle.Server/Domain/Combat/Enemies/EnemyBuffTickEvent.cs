using BlazorWebGame.Domain.Combat;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

/// <summary>
/// 敌人 Buff Tick 事件
/// 定期更新所有敌人的 Buff 状态，处理 DoT/HoT 等周期效果
/// </summary>
public record EnemyBuffTickEvent(
    double ExecuteAt,
    double Interval) : IGameEvent
{
    public string EventType => "EnemyBuffTick";

    public void Execute(BattleContext context)
    {
        // Tick 所有敌人的 BuffManager
        bool hasActiveBuffs = false;
        foreach (var enemy in context.EnemyCombatants)
        {
            if (enemy.Buffs != null && enemy.CanAct())
            {
                enemy.Buffs.Tick(ExecuteAt);
                
                // Check if this enemy has any active buffs
                if (enemy.Buffs.Active.Any())
                {
                    hasActiveBuffs = true;
                }
            }
        }

        // Only schedule next tick if there are active buffs
        // This reduces event count when no buffs are active
        if (hasActiveBuffs || ExecuteAt < 1.0)  // Always tick for first second to catch early buffs
        {
            context.Scheduler.Schedule(new EnemyBuffTickEvent(ExecuteAt + Interval, Interval));
        }
    }
}
