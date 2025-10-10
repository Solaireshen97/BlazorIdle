using BlazorIdle.Server.Domain.Combat.Damage;
using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public static class EnemyRegistry
{
    // Phase 5: 技能定义
    private static readonly List<EnemySkillDefinition> _dummySkills = new()
    {
        // 重击：10秒CD，造成2倍普通攻击的伤害
        new EnemySkillDefinition(
            id: "dummy_heavy_strike",
            name: "Heavy Strike",
            cooldownSeconds: 10.0,
            trigger: TriggerType.OnCooldownReady,
            effect: SkillEffectType.Damage,
            effectValue: 10,  // 2倍基础伤害(5*2)
            damageType: DamageType.Physical
        )
    };

    private static readonly List<EnemySkillDefinition> _tankSkills = new()
    {
        // 重击：12秒CD，造成额外伤害
        new EnemySkillDefinition(
            id: "tank_power_slam",
            name: "Power Slam",
            cooldownSeconds: 12.0,
            trigger: TriggerType.OnCooldownReady,
            effect: SkillEffectType.Damage,
            effectValue: 20,
            damageType: DamageType.Physical
        ),
        // 愤怒：HP低于30%时触发（仅一次），提升自身（通过Buff标签记录）
        new EnemySkillDefinition(
            id: "tank_enrage",
            name: "Enrage",
            cooldownSeconds: 5.0,
            trigger: TriggerType.OnHpBelow,
            triggerValue: 0.30,  // 30% HP
            effect: SkillEffectType.ApplyBuff,
            buffId: "enrage"
        )
    };

    private static readonly List<EnemySkillDefinition> _magebaneSkills = new()
    {
        // 魔法爆发：8秒CD，造成魔法伤害
        new EnemySkillDefinition(
            id: "magebane_arcane_burst",
            name: "Arcane Burst",
            cooldownSeconds: 8.0,
            trigger: TriggerType.OnCooldownReady,
            effect: SkillEffectType.Damage,
            effectValue: 25,
            damageType: DamageType.Magic
        ),
        // 恢复：战斗30秒后触发一次，恢复50% HP
        new EnemySkillDefinition(
            id: "magebane_regeneration",
            name: "Regeneration",
            cooldownSeconds: 60.0,  // 长CD确保只触发一次
            trigger: TriggerType.OnCombatTimeElapsed,
            triggerValue: 30.0,  // 30秒后
            effect: SkillEffectType.Heal,
            effectValue: 190  // 50% of 380 HP
        )
    };

    private static readonly Dictionary<string, EnemyDefinition> _defs = new()
    {
        // Phase 4: 添加怪物攻击属性
        // Phase 5: 添加怪物技能
        // 伤害平衡：玩家默认 100 HP (10 stamina)，10秒复活
        // 怪物伤害约为 5 DPS，确保玩家不会瞬间死亡
        ["dummy"] = new EnemyDefinition(
            id: "dummy",
            name: "Training Dummy",
            level: 1,
            maxHp: 150,
            armor: 50,
            magicResist: 0.1,
            baseDamage: 5,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 3.0,
            skills: _dummySkills
        ),
        ["tank"] = new EnemyDefinition(
            id: "tank",
            name: "Armored Golem",
            level: 10,
            maxHp: 500,
            armor: 400,
            magicResist: 0.15,
            baseDamage: 8,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 2.5,
            skills: _tankSkills
        ),
        ["magebane"] = new EnemyDefinition(
            id: "magebane",
            name: "Magebane Warden",
            level: 10,
            maxHp: 380,
            armor: 120,
            magicResist: 0.35,
            baseDamage: 10,
            attackDamageType: DamageType.Magic,
            attackIntervalSeconds: 2.0,
            skills: _magebaneSkills
        ),
        ["paper"] = new EnemyDefinition(
            id: "paper",
            name: "Paper Slime",
            level: 1,
            maxHp: 80,
            armor: 0,
            magicResist: 0.0,
            vulnPhys: 0.1,
            vulnMagic: 0.1,
            baseDamage: 3,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 4.0
        ),
    };

    public static EnemyDefinition Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return _defs["dummy"];
        return _defs.TryGetValue(id, out var d) ? d : _defs["dummy"];
    }

    public static IEnumerable<EnemyDefinition> All() => _defs.Values;
}