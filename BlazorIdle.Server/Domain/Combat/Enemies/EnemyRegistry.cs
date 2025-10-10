using BlazorIdle.Server.Domain.Combat.Damage;
using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public static class EnemyRegistry
{
    private static readonly Dictionary<string, EnemyDefinition> _defs = CreateEnemies();
    
    private static Dictionary<string, EnemyDefinition> CreateEnemies()
    {
        // Phase 4: 添加怪物攻击属性
        // Phase 5: 添加怪物技能
        // 伤害平衡：玩家默认 100 HP (10 stamina)，10秒复活
        // 怪物伤害约为 5 DPS，确保玩家不会瞬间死亡
        
        var dummy = new EnemyDefinition(
            id: "dummy",
            name: "Training Dummy",
            level: 1,
            maxHp: 150,
            armor: 50,
            magicResist: 0.1,
            baseDamage: 5,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 3.0
        );
        
        var tank = new EnemyDefinition(
            id: "tank",
            name: "Armored Golem",
            level: 10,
            maxHp: 500,
            armor: 400,
            magicResist: 0.15,
            baseDamage: 8,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 2.5
        );
        // Phase 5: Tank 技能 - 重击（每10秒造成双倍伤害）
        tank.Skills.Add(new EnemySkillDefinition(
            id: "heavy_strike",
            name: "Heavy Strike",
            cooldownSeconds: 10.0,
            trigger: TriggerType.OnCooldownReady,
            effect: SkillEffectType.Damage,
            effectValue: 20,  // 额外造成 20 点伤害
            damageType: DamageType.Physical
        ));
        
        var magebane = new EnemyDefinition(
            id: "magebane",
            name: "Magebane Warden",
            level: 10,
            maxHp: 380,
            armor: 120,
            magicResist: 0.35,
            baseDamage: 10,
            attackDamageType: DamageType.Magic,
            attackIntervalSeconds: 2.0
        );
        // Phase 5: Magebane 技能 - 法术爆发（战斗30秒后一次性高额魔法伤害）
        magebane.Skills.Add(new EnemySkillDefinition(
            id: "spell_burst",
            name: "Spell Burst",
            cooldownSeconds: 999.0,  // 高冷却确保只触发一次
            trigger: TriggerType.OnCombatTimeElapsed,
            triggerValue: 30.0,  // 战斗30秒后触发
            effect: SkillEffectType.Damage,
            effectValue: 35,
            damageType: DamageType.Magic
        ));
        
        var paper = new EnemyDefinition(
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
        );
        // Phase 5: Paper Slime 技能 - 绝望一击（HP低于30%时触发，造成额外伤害）
        paper.Skills.Add(new EnemySkillDefinition(
            id: "desperate_strike",
            name: "Desperate Strike",
            cooldownSeconds: 0.0,  // 无冷却，仅依赖HP触发条件
            trigger: TriggerType.OnHpBelow,
            triggerValue: 0.3,  // HP < 30% 时触发
            effect: SkillEffectType.Damage,
            effectValue: 15,
            damageType: DamageType.Physical
        ));
        
        return new Dictionary<string, EnemyDefinition>
        {
            ["dummy"] = dummy,
            ["tank"] = tank,
            ["magebane"] = magebane,
            ["paper"] = paper,
        };
    }

    public static EnemyDefinition Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return _defs["dummy"];
        return _defs.TryGetValue(id, out var d) ? d : _defs["dummy"];
    }

    public static IEnumerable<EnemyDefinition> All() => _defs.Values;
}