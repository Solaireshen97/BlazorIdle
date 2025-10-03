namespace BlazorIdle.Server.Domain.Combat.Buffs;

public static class BuffDefinitionsRegistry
{
    // 示例：战士技能加速 Buff（持续 6 秒，刷新型）
    public static BuffDefinition WarriorBerserk => new(
        id: "berserk",
        name: "Berserk",
        durationSeconds: 6,
        maxStacks: 1,
        stackPolicy: BuffStackPolicy.Refresh,
        additiveHaste: 0.3 // +0.3 hasteFactor
    );

    // 示例：游侠持续 DoT（叠加伤害）
    public static BuffDefinition RangerBleed => new(
        id: "ranger_bleed",
        name: "Ranger Bleed",
        durationSeconds: 8,
        maxStacks: 3,
        stackPolicy: BuffStackPolicy.Stack,
        periodicType: BuffPeriodicType.Damage,
        periodicInterval: 2,
        periodicValue: 15
    );

    // 示例：专注回复（周期性回资源，Extend 策略）
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
}