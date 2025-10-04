using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat.Buffs;

public static class BuffDefinitionsRegistry
{
    public static BuffDefinition WarriorBerserk => new(
        id: "berserk",
        name: "Berserk",
        durationSeconds: 6,
        maxStacks: 1,
        stackPolicy: BuffStackPolicy.Refresh,
        additiveHaste: 0.3
    );

    // DoT：游侠流血（物理 DoT，2 秒一跳，每跳 15），Haste 加速，Pandemic 30%
    public static BuffDefinition RangerBleed => new(
        id: "ranger_bleed",
        name: "Ranger Bleed",
        durationSeconds: 8,
        maxStacks: 3,
        stackPolicy: BuffStackPolicy.Stack,
        periodicType: BuffPeriodicType.Damage,
        periodicInterval: 2,
        periodicValue: 15,
        periodicResourceId: null,
        periodicDamageType: DamageType.Physical,
        periodicHasteAffected: true,
        pandemicRatio: 0.30
    );

    public static BuffDefinition FocusFlow => new(
        id: "focus_flow",
        name: "Focus Flow",
        durationSeconds: 5,
        maxStacks: 3,
        stackPolicy: BuffStackPolicy.Extend,
        periodicType: BuffPeriodicType.Resource,
        periodicInterval: 1,
        periodicValue: 3,
        periodicResourceId: "focus"
    );

    public static BuffDefinition WarriorExposeArmor => new(
        id: "warrior_expose_armor",
        name: "Expose Armor",
        durationSeconds: 8,
        maxStacks: 1,
        stackPolicy: BuffStackPolicy.Refresh,
        armorPenFlat: 150
    );

    public static BuffDefinition RangerHuntersMark => new(
        id: "ranger_hunters_mark",
        name: "Hunter's Mark",
        durationSeconds: 10,
        maxStacks: 1,
        stackPolicy: BuffStackPolicy.Refresh,
        damageMultiplierPhysical: 0.10
    );

    public static BuffDefinition WarriorPrecision => new(
        id: "warrior_precision",
        name: "Precision",
        durationSeconds: 6,
        maxStacks: 1,
        stackPolicy: BuffStackPolicy.Refresh,
        critChanceBonus: 0.20
    );

    public static BuffDefinition RangerSharpsight => new(
        id: "ranger_sharpsight",
        name: "Sharpsight",
        durationSeconds: 8,
        maxStacks: 1,
        stackPolicy: BuffStackPolicy.Refresh,
        critMultiplierBonus: 0.50
    );
}