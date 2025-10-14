using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using System;
using System.Linq;
using Xunit;

public class DoTSkillTests
{
    [Fact]
    public void BleedShot_Applies_RangerBleed_And_Ticks_Damage()
    {
        var simulator = new BattleSimulator();
        var runner = new BattleRunner(simulator);
        var seed = 1234UL;
        var duration = 50.0;

        var battleA = new Battle { CharacterId = Guid.NewGuid(), AttackIntervalSeconds = 1.4, SpecialIntervalSeconds = 4.0, StartedAt = 0 };
        var battleB = new Battle { CharacterId = battleA.CharacterId, AttackIntervalSeconds = 1.4, SpecialIntervalSeconds = 4.0, StartedAt = 0 };

        var modNoBleed = new RangerNoBleed(); // 自定义：移除 bleed_shot
        var modWithBleed = new BlazorIdle.Server.Domain.Combat.Professions.RangerProfession();

        var encA = new Encounter(EnemyRegistry.Resolve("dummy"));
        var encB = new Encounter(EnemyRegistry.Resolve("dummy"));

        var segA = runner.RunForDuration(battleA, duration, Profession.Ranger, new RngContext(seed), out _, out _, out _, modNoBleed, encA);
        var segB = runner.RunForDuration(battleB, duration, Profession.Ranger, new RngContext(seed), out _, out _, out _, modWithBleed, encB);

        int dotTicksA = segA.Sum(s => s.TagCounters.TryGetValue("buff_tick:ranger_bleed", out var v) ? v : 0);
        int dotTicksB = segB.Sum(s => s.TagCounters.TryGetValue("buff_tick:ranger_bleed", out var v) ? v : 0);

        Assert.True(dotTicksB > dotTicksA, "含 bleed_shot 的战斗应出现更多流血 Tick");

        int dmgA = segA.Sum(s => s.TotalDamage);
        int dmgB = segB.Sum(s => s.TotalDamage);
        Assert.True(dmgB > dmgA, "含 bleed_shot 的总伤应更高");
    }

    private sealed class RangerNoBleed : BlazorIdle.Server.Domain.Combat.Professions.IProfessionModule
    {
        private readonly BlazorIdle.Server.Domain.Combat.Professions.RangerProfession _base = new();

        public string Id => _base.Id;
        public double BaseAttackInterval => _base.BaseAttackInterval;
        public double BaseSpecialInterval => _base.BaseSpecialInterval;
        public bool PauseSpecialWhenNoEnemies => _base.PauseSpecialWhenNoEnemies;
        public bool SpecialStartsImmediately => _base.SpecialStartsImmediately;
        public void RegisterBuffDefinitions(BlazorIdle.Server.Domain.Combat.BattleContext context) => _base.RegisterBuffDefinitions(context);
        public void OnBattleStart(BlazorIdle.Server.Domain.Combat.BattleContext context) => _base.OnBattleStart(context);
        public void OnAttackTick(BlazorIdle.Server.Domain.Combat.BattleContext context, BlazorIdle.Server.Domain.Combat.AttackTickEvent evt) => _base.OnAttackTick(context, evt);
        public void OnSpecialPulse(BlazorIdle.Server.Domain.Combat.BattleContext context, BlazorIdle.Server.Domain.Combat.SpecialPulseEvent evt) => _base.OnSpecialPulse(context, evt);

        // 仅保留 power / quick，不添加 bleed_shot
        public void BuildSkills(BlazorIdle.Server.Domain.Combat.BattleContext context, BlazorIdle.Server.Domain.Combat.Skills.AutoCastEngine engine)
        {
            engine.AddSkill(new BlazorIdle.Server.Domain.Combat.Skills.SkillDefinition(
                id: "power_shot", name: "Power Shot",
                costResourceId: "focus", costAmount: 40,
                cooldownSeconds: 5.0, priority: 5,
                baseDamage: 70, critChance: 0.25, critMultiplier: 2.4
            ));
            engine.AddSkill(new BlazorIdle.Server.Domain.Combat.Skills.SkillDefinition(
                id: "quick_shot", name: "Quick Shot",
                costResourceId: "focus", costAmount: 10,
                cooldownSeconds: 1.5, priority: 15,
                baseDamage: 25, critChance: 0.05, critMultiplier: 1.8
            ));
        }

        // 沿用基类逻辑；因为没有 bleed_shot，不会触发该分支
        public void OnSkillCast(BlazorIdle.Server.Domain.Combat.BattleContext context, BlazorIdle.Server.Domain.Combat.Skills.SkillDefinition def)
            => _base.OnSkillCast(context, def);
    }
}