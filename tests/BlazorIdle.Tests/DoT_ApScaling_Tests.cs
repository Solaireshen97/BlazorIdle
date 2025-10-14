using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using System;
using System.Linq;
using Xunit;

public class DoT_ApScaling_Tests
{
    private sealed class TestApDotProfession : IProfessionModule
    {
        public string Id => "test-ap-dot";
        public double BaseAttackInterval => 999; // 避免普攻干扰
        public double BaseSpecialInterval => 999;
        
        // 默认测试行为
        public bool PauseSpecialWhenNoEnemies => true;
        public bool SpecialStartsImmediately => false;
        public bool SpecialStartsImmediatelyAfterRevive => false;

        public void RegisterBuffDefinitions(BattleContext context)
        {
            // 2s 一跳，8s 持续；每跳基础 10；吃 AP 系数 0.2
            var def = new BuffDefinition(
                id: "dot_ap_test",
                name: "DoT AP Test",
                durationSeconds: 8,
                maxStacks: 1,
                stackPolicy: BuffStackPolicy.Refresh,
                periodicType: BuffPeriodicType.Damage,
                periodicInterval: 2.0,
                periodicValue: 10,
                periodicResourceId: null,
                periodicDamageType: BlazorIdle.Server.Domain.Combat.Damage.DamageType.Physical,
                periodicHasteAffected: true,
                pandemicRatio: 0.30,
                periodicApCoef: 0.20,
                periodicSpCoef: 0.0
            );
            context.Buffs.RegisterDefinition(def);
        }

        public void OnBattleStart(BattleContext context)
        {
            context.Buffs.Apply("dot_ap_test", context.Clock.CurrentTime);
        }

        public void OnAttackTick(BattleContext context, AttackTickEvent evt) { }
        public void OnSpecialPulse(BattleContext context, SpecialPulseEvent evt) { }
        public void BuildSkills(BattleContext context, BlazorIdle.Server.Domain.Combat.Skills.AutoCastEngine engine) { }
        public void OnSkillCast(BattleContext context, BlazorIdle.Server.Domain.Combat.Skills.SkillDefinition def) { }
    }

    [Fact]
    public void Dot_Deals_More_Total_Damage_With_Higher_AP_But_Same_Tick_Count()
    {
        var simulator = new BattleSimulator();
        var runner = new BattleRunner(simulator);
        var seed = new RngContext(20251004);

        // A: AP = 0
        var battleA = new Battle { CharacterId = Guid.NewGuid(), AttackIntervalSeconds = 999, SpecialIntervalSeconds = 999, StartedAt = 0 };
        var statsA = new CharacterStats { AttackPower = 0, SpellPower = 0, HastePercent = 0 };
        var encA = new Encounter(EnemyRegistry.Resolve("dummy"));
        var segA = runner.RunForDuration(battleA, durationSeconds: 8.1, profession: Profession.Warrior, rng: new RngContext(seed.NextUInt64()),
                                         killed: out _, killTime: out _, overkill: out _, module: new TestApDotProfession(),
                                         encounter: encA, encounterGroup: null, stats: statsA);

        // B: AP = 200
        var battleB = new Battle { CharacterId = battleA.CharacterId, AttackIntervalSeconds = 999, SpecialIntervalSeconds = 999, StartedAt = 0 };
        var statsB = new CharacterStats { AttackPower = 200, SpellPower = 0, HastePercent = 0 };
        var encB = new Encounter(EnemyRegistry.Resolve("dummy"));
        var segB = runner.RunForDuration(battleB, durationSeconds: 8.1, profession: Profession.Warrior, rng: new RngContext(seed.NextUInt64()),
                                         killed: out _, killTime: out _, overkill: out _, module: new TestApDotProfession(),
                                         encounter: encB, encounterGroup: null, stats: statsB);

        // 统计 tick 次数与总伤
        int ticksA = segA.Sum(s => s.TagCounters.TryGetValue("buff_tick:dot_ap_test", out var v) ? v : 0);
        int ticksB = segB.Sum(s => s.TagCounters.TryGetValue("buff_tick:dot_ap_test", out var v) ? v : 0);
        int totalA = segA.Sum(s => s.TotalDamage);
        int totalB = segB.Sum(s => s.TotalDamage);

        Assert.Equal(ticksA, ticksB); // 相同 Haste，tick 次数应一致
        Assert.True(totalB > totalA, $"Higher AP should increase dot damage: {totalA} -> {totalB}");
    }
}