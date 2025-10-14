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

    void RegisterBuffDefinitions(BattleContext context);          // 新增：每个职业注册自己需要的 BuffDefinition

    /// <summary>
    /// 特殊轨道是否在无怪物（等待刷新）时暂停
    /// true: 跟随攻击轨道暂停（默认行为）
    /// false: 持续触发，不受怪物存在影响
    /// </summary>
    bool PauseSpecialWhenNoEnemies { get; }
    
    /// <summary>
    /// 特殊轨道的初始延迟行为
    /// true: 战斗开始后立即触发（从0开始）
    /// false: 等待完整间隔后触发（从 specialInterval 开始，默认）
    /// </summary>
    bool SpecialStartsImmediately { get; }

}