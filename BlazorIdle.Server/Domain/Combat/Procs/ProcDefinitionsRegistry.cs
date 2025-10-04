using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Skills;

namespace BlazorIdle.Server.Domain.Combat.Procs;

public static class ProcDefinitionsRegistry
{
    // 旧示例：OnCrit → 单体额外伤害
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

    // 旧示例：OnHit → 上破甲
    public static ProcDefinition WarriorShredOnHit => new(
        id: "warrior_shred_on_hit",
        name: "Shred Armor",
        trigger: ProcTriggerType.OnHit,
        chance: 0.20,
        icdSeconds: 3.0,
        sourceFilter: ProcSourceFilter.BasicAttackOnly,
        allowFromDot: false,
        damageTypeFilter: DamageType.Physical,
        action: ProcActionType.ApplyBuff,
        actionBuffId: BuffDefinitionsRegistry.WarriorExposeArmor.Id
    );

    // 旧示例：RPPM → 单体奥术伤害
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

    // 新增示例：OnCrit → AoE（CleaveFull）3 目标
    public static ProcDefinition RangerExplosiveBurst => new(
        id: "ranger_explosive_burst",
        name: "Explosive Burst",
        trigger: ProcTriggerType.OnCrit,
        chance: 1.0,
        icdSeconds: 0.0,
        sourceFilter: ProcSourceFilter.SkillOnly,
        allowFromDot: false,
        damageTypeFilter: null,
        action: ProcActionType.DealDamage,
        actionBuffId: null,
        actionDamageValue: 18,
        actionDamageType: DamageType.Magic,
        maxTargets: 3,
        aoeMode: AoEMode.CleaveFull,
        includePrimaryTarget: true,
        splitRemainderToPrimary: true
    );
}