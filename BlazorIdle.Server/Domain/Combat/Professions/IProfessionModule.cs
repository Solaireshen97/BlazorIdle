using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Skills;

namespace BlazorIdle.Server.Domain.Combat.Professions;

public interface IProfessionModule
{
    string Id { get; }
    double BaseAttackInterval { get; }
    double BaseSpecialInterval { get; }

    void OnBattleStart(BattleContext context);
    void OnAttackTick(BattleContext context, AttackTickEvent evt);
    void OnSpecialPulse(BattleContext context, SpecialPulseEvent evt);

    // 新增：构建技能
    void BuildSkills(BattleContext context, AutoCastEngine engine);

    // 新增：技能施放后额外处理（如 Buff、额外资源等）
    void OnSkillCast(BattleContext context, Skills.SkillDefinition def);
}