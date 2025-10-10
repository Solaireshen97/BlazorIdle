using BlazorWebGame.Domain.Combat;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

/// <summary>
/// 怪物技能检查事件：定期检查所有怪物是否有可释放的技能
/// Phase 5: 怪物技能系统
/// </summary>
public record EnemySkillCheckEvent(double ExecuteAt, double CheckInterval) : IGameEvent
{
    public string EventType => "EnemySkillCheck";

    public void Execute(BattleContext context)
    {
        // 遍历所有有技能管理器的怪物
        foreach (var enemyCombatant in context.EnemyCombatants.Where(e => e.SkillManager != null))
        {
            // 检查怪物是否还存活且可以行动
            if (!enemyCombatant.CanAct())
            {
                continue;
            }

            var skillManager = enemyCombatant.SkillManager!;
            
            // 检查是否有可以触发的技能
            var readySkill = skillManager.CheckForReadySkill(ExecuteAt);
            if (readySkill != null)
            {
                // 调度技能释放事件
                context.Scheduler.Schedule(new EnemySkillCastEvent(
                    ExecuteAt,
                    enemyCombatant,
                    readySkill
                ));
            }
        }

        // 调度下一次检查（定期检查）
        context.Scheduler.Schedule(new EnemySkillCheckEvent(ExecuteAt + CheckInterval, CheckInterval));
    }
}
