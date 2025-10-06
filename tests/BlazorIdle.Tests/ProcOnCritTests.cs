using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using System;
using System.Linq;
using Xunit;

public class ProcOnCritTests
{
    private sealed class RangerNoProc : BlazorIdle.Server.Domain.Combat.Professions.RangerProfession
    {
        public override void OnBattleStart(BattleContext context)
        {
            base.OnBattleStart(context);
            // 故意不注册爆裂箭 Proc
        }
    }

    private sealed class RangerWithProc : BlazorIdle.Server.Domain.Combat.Professions.RangerProfession
    {
        public override void OnBattleStart(BattleContext context)
        {
            base.OnBattleStart(context);
            context.Procs.RegisterDefinition(ProcDefinitionsRegistry.RangerExplosiveArrow);
        }
    }

    [Fact]
    public void ExplosiveArrow_OnCrit_Increases_Damage_And_Tags_Proc()
    {
        var simulator = new BattleSimulator();
        var runner = new BattleRunner(simulator);
        var seed = 987654UL;

        var battleA = new Battle { CharacterId = Guid.NewGuid(), AttackIntervalSeconds = 1.4, SpecialIntervalSeconds = 4.0, StartedAt = 0 };
        var battleB = new Battle { CharacterId = battleA.CharacterId, AttackIntervalSeconds = 1.4, SpecialIntervalSeconds = 4.0, StartedAt = 0 };

        var rngA = new RngContext(seed);
        var rngB = new RngContext(seed);

        var encA = new Encounter(EnemyRegistry.Resolve("dummy"));
        var encB = new Encounter(EnemyRegistry.Resolve("dummy"));

        var segA = runner.RunForDuration(battleA, 12, Profession.Ranger, rngA, out _, out _, out _, new RangerNoProc(), encA);
        var segB = runner.RunForDuration(battleB, 12, Profession.Ranger, rngB, out _, out _, out _, new RangerWithProc(), encB);

        int dmgA = segA.Sum(s => s.TotalDamage);
        int dmgB = segB.Sum(s => s.TotalDamage);

        int procsB = segB.Sum(s => s.TagCounters.TryGetValue("proc:ranger_explosive_arrow", out var v) ? v : 0);

        Assert.True(procsB > 0, "应出现 Explosive Arrow 的触发标签");
        Assert.True(dmgB > dmgA, $"有 Proc 时总伤应更高：{dmgA} -> {dmgB}");
    }
}