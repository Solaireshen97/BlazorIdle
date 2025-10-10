using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat.Buffs;

/// <summary>
/// 敌人 Buff 定义注册表
/// 为怪物技能系统提供 Buff 定义
/// </summary>
public static class EnemyBuffDefinitionsRegistry
{
    /// <summary>
    /// 获取所有敌人 Buff 定义
    /// </summary>
    public static List<BuffDefinition> GetAll()
    {
        return new List<BuffDefinition>
        {
            // Enrage: 增加物理伤害 50%，持续 15 秒
            new BuffDefinition(
                id: "enrage",
                name: "Enrage",
                durationSeconds: 15.0,
                maxStacks: 1,
                stackPolicy: BuffStackPolicy.Refresh,
                damageMultiplierPhysical: 0.5  // +50% physical damage
            ),
            
            // Poison: 持续伤害效果，每 2 秒造成 5 点伤害，持续 10 秒
            new BuffDefinition(
                id: "poison",
                name: "Poison",
                durationSeconds: 10.0,
                maxStacks: 3,
                stackPolicy: BuffStackPolicy.Stack,
                periodicType: BuffPeriodicType.Damage,
                periodicInterval: 2.0,
                periodicValue: 5,
                periodicDamageType: DamageType.True
            ),
            
            // Regeneration: 持续治疗效果，每 2 秒恢复 10 点生命值，持续 20 秒
            new BuffDefinition(
                id: "regeneration",
                name: "Regeneration",
                durationSeconds: 20.0,
                maxStacks: 1,
                stackPolicy: BuffStackPolicy.Refresh,
                periodicType: BuffPeriodicType.Resource,
                periodicInterval: 2.0,
                periodicValue: 10,
                periodicResourceId: "health"  // 用于标识治疗效果
            ),
            
            // Haste: 增加攻击速度 30%，持续 12 秒
            new BuffDefinition(
                id: "haste",
                name: "Haste",
                durationSeconds: 12.0,
                maxStacks: 1,
                stackPolicy: BuffStackPolicy.Refresh,
                additiveHaste: 0.3  // +30% haste
            ),
            
            // Armor Up: 增加护甲，持续 15 秒（通过伤害减免实现）
            new BuffDefinition(
                id: "armor_up",
                name: "Armor Up",
                durationSeconds: 15.0,
                maxStacks: 1,
                stackPolicy: BuffStackPolicy.Refresh,
                damageMultiplierPhysical: -0.25  // -25% physical damage taken (defensive buff)
            ),
            
            // Magic Shield: 增加魔法抗性，持续 15 秒
            new BuffDefinition(
                id: "magic_shield",
                name: "Magic Shield",
                durationSeconds: 15.0,
                maxStacks: 1,
                stackPolicy: BuffStackPolicy.Refresh,
                damageMultiplierMagic: -0.3  // -30% magic damage taken
            ),
            
            // Berserk: 增加所有伤害 75%，持续 10 秒
            new BuffDefinition(
                id: "berserk",
                name: "Berserk",
                durationSeconds: 10.0,
                maxStacks: 1,
                stackPolicy: BuffStackPolicy.Refresh,
                damageMultiplierPhysical: 0.75,
                damageMultiplierMagic: 0.75,
                damageMultiplierTrue: 0.75
            )
        };
    }
}
