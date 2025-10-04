using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat.Procs;

public static class ProcDefinitionsRegistry
{
    // 示例1：游侠“爆裂箭”——暴击时 100% 触发，额外造成 20 物理伤害（单体）
    public static ProcDefinition RangerExplosiveArrow => new(
        id: "ranger_explosive_arrow",
        name: "Explosive Arrow",
        trigger: ProcTriggerType.OnCrit,
        chance: 1.0,
        icdSeconds: 0.0,
        sourceFilter: ProcSourceFilter.SkillOnly,
        allowFromDot: false,
        damageTypeFilter: null,
        action: ProcActionType.DealDamage,
        actionBuffId: null,
        actionDamageValue: 20,
        actionDamageType: DamageType.Physical
    );

    // 示例2：战士“破甲打击”——OnHit 20% 几率上 Expose Armor，ICD 3s
    public static ProcDefinition WarriorShredOnHit => new(
        id: "warrior_shred_on_hit",
        name: "Shred Armor",
        trigger: ProcTriggerType.OnHit,
        chance: 0.20,
        icdSeconds: 3.0,
        sourceFilter: ProcSourceFilter.BasicAttackOnly, // 仅普攻
        allowFromDot: false,
        damageTypeFilter: DamageType.Physical,
        action: ProcActionType.ApplyBuff,
        actionBuffId: BuffDefinitionsRegistry.WarriorExposeArmor.Id
    );

    // 示例3：RPPM——每分钟 1 次，触发时造成 15 点魔法伤害
    public static ProcDefinition ArcanePulseRppm => new(
        id: "arcane_pulse_rppm",
        name: "Arcane Pulse",
        trigger: ProcTriggerType.Rppm,
        rppm: 1.0,
        icdSeconds: 0.0,
        sourceFilter: ProcSourceFilter.Any,
        allowFromDot: true,
        damageTypeFilter: null,
        action: ProcActionType.DealDamage,
        actionBuffId: null,
        actionDamageValue: 15,
        actionDamageType: DamageType.Magic
    );
}