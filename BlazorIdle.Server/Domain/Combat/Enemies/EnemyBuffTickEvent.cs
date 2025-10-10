using BlazorWebGame.Domain.Combat;

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
        foreach (var enemy in context.EnemyCombatants)
        {
            if (enemy.Buffs != null && enemy.CanAct())
            {
                enemy.Buffs.Tick(ExecuteAt);
            }
        }

        // 递归调度下一次 Buff Tick
        context.Scheduler.Schedule(new EnemyBuffTickEvent(ExecuteAt + Interval, Interval));
    }
}
