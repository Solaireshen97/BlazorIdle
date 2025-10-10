using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat.Buffs;

/// <summary>
/// 怪物专属 Buff 定义注册表
/// 用于存储和管理怪物可以获得的增益效果
/// </summary>
public static class EnemyBuffDefinitionsRegistry
{
    /// <summary>
    /// 狂怒 Buff：低血量时触发，提升攻击力
    /// </summary>
    public static BuffDefinition Enrage => new(
        id: "enemy_enrage",
        name: "Enrage",
        durationSeconds: 20,
        maxStacks: 1,
        stackPolicy: BuffStackPolicy.Refresh,
        damageMultiplierPhysical: 0.50, // +50% 物理伤害
        damageMultiplierMagic: 0.50     // +50% 魔法伤害
    );

    /// <summary>
    /// 护甲强化 Buff：提供额外防护
    /// </summary>
    public static BuffDefinition ArmorBoost => new(
        id: "enemy_armor_boost",
        name: "Armor Boost",
        durationSeconds: 15,
        maxStacks: 1,
        stackPolicy: BuffStackPolicy.Refresh
        // 注意：当前怪物不受伤害，此 Buff 主要用于演示
        // 未来可扩展怪物防御属性修饰
    );

    /// <summary>
    /// 急速 Buff：提升攻击速度
    /// </summary>
    public static BuffDefinition Haste => new(
        id: "enemy_haste",
        name: "Haste",
        durationSeconds: 10,
        maxStacks: 1,
        stackPolicy: BuffStackPolicy.Refresh,
        additiveHaste: 0.30 // +30% 急速
    );

    /// <summary>
    /// 狂暴 Buff：多层叠加，大幅提升伤害
    /// </summary>
    public static BuffDefinition Frenzy => new(
        id: "enemy_frenzy",
        name: "Frenzy",
        durationSeconds: 8,
        maxStacks: 5,
        stackPolicy: BuffStackPolicy.Stack,
        damageMultiplierPhysical: 0.20, // 每层 +20% 物理伤害
        damageMultiplierMagic: 0.20     // 每层 +20% 魔法伤害
    );

    /// <summary>
    /// 精准 Buff：提升暴击率
    /// </summary>
    public static BuffDefinition Precision => new(
        id: "enemy_precision",
        name: "Precision",
        durationSeconds: 12,
        maxStacks: 1,
        stackPolicy: BuffStackPolicy.Refresh,
        critChanceBonus: 0.25 // +25% 暴击率
    );

    /// <summary>
    /// 破甲 Buff：降低目标护甲（用于未来扩展）
    /// </summary>
    public static BuffDefinition SunderArmor => new(
        id: "enemy_sunder_armor",
        name: "Sunder Armor",
        durationSeconds: 10,
        maxStacks: 3,
        stackPolicy: BuffStackPolicy.Stack,
        armorPenFlat: 100, // 每层 -100 护甲
        armorPenPct: 0.10  // 每层 -10% 护甲百分比
    );
}
