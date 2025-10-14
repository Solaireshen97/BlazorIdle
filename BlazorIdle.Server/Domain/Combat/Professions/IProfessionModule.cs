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
    /// 战斗循环优化 - 中篇：特殊轨道是否在无怪物（等待刷新）时暂停
    /// true: 特殊轨道跟随攻击轨道暂停（默认行为）
    /// false: 特殊轨道持续触发，不受怪物存在影响
    /// null: 使用 CombatLoopOptions.PauseSpecialWhenNoEnemiesByDefault 配置
    /// </summary>
    bool? PauseSpecialWhenNoEnemies => null;
    
    /// <summary>
    /// 战斗循环优化 - 中篇：特殊轨道在战斗开始/恢复时是否立即触发
    /// true: 立即触发（从 0 开始）
    /// false: 等待完整间隔后触发（从 specialInterval 开始）
    /// null: 使用 CombatLoopOptions.SpecialStartsWithFullInterval 配置的反向逻辑
    /// </summary>
    bool? SpecialStartsImmediately => null;
    
    /// <summary>
    /// 战斗循环优化 - 中篇：特殊轨道在玩家复活后是否立即触发
    /// true: 复活后立即触发特殊轨道（从 0 开始）
    /// false: 复活后等待完整间隔（从 specialInterval 开始）
    /// null: 使用 CombatLoopOptions.SpecialStartsImmediatelyAfterReviveByDefault 配置
    /// </summary>
    bool? SpecialStartsImmediatelyAfterRevive => null;

}