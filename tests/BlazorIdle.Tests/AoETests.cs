using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Skills;
using BlazorIdle.Shared.Models;
using System;
using System.Linq;
using Xunit;

public class AoETests
{
    private sealed class TestAoEProfession : BlazorIdle.Server.Domain.Combat.Professions.RangerProfession
    {
        public override void BuildSkills(BattleContext context, AutoCastEngine engine)
        {
            // 单体对照
            engine.AddSkill(new SkillDefinition(
                id: "single",
                name: "Single",
                costResourceId: null,
                costAmount: 0,
                cooldownSeconds: 1000, // 基本不再触发
                priority: 5,
                baseDamage: 120
            ));

            // CleaveFull：对 3 个目标各打 60
            engine.AddSkill(new SkillDefinition(
                id: "cleave",
                name: "Cleave",
                costResourceId: null,
                costAmount: 0,
                cooldownSeconds: 1.0,
                priority: 1,
                baseDamage: 60,
                maxTargets: 3,
                aoeMode: AoEMode.CleaveFull
            ));
        }
    }

    [Fact]
    public void CleaveFull_Hits_Multiple_Targets()
    {
        var simulator = new BattleSimulator();
        var runner = new BattleRunner(simulator);
        var battle = new Battle { CharacterId = Guid.NewGuid(), AttackIntervalSeconds = 999, SpecialIntervalSeconds = 999, StartedAt = 0 };
        var rng = new RngContext(123);
        var group = new EncounterGroup(new[]
        {
            EnemyRegistry.Resolve("paper"),
            EnemyRegistry.Resolve("paper"),
            EnemyRegistry.Resolve("paper")
        });

        var segments = runner.RunForDuration(battle, 1.0, Profession.Ranger, rng, out _, out _, out _, new TestAoEProfession(), null, group);

        var total = segments.Sum(s => s.TotalDamage);
        // 基本要求：总伤至少接近 3*60（paper 无减伤；忽略浮动/暴击则 >= 180）
        Assert.True(total >= 180, $"Total damage was {total}");
    }
}