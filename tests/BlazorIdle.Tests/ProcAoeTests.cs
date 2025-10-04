using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Skills;
using BlazorIdle.Shared.Models;
using System;
using System.Linq;
using Xunit;

public class ProcAoeTests
{
    private sealed class TestAoeProcProfession : BlazorIdle.Server.Domain.Combat.Professions.RangerProfession
    {
        public override void OnBattleStart(BattleContext context)
        {
            base.OnBattleStart(context);
            // 注册一个 100% OnHit 的 AoE Proc（3 目标 CleaveFull，单跳 10 伤）
            var aoeProc = new ProcDefinition(
                id: "test_aoe_proc",
                name: "Test AoE Proc",
                trigger: ProcTriggerType.OnHit,
                chance: 1.0,
                icdSeconds: 0.0,
                sourceFilter: ProcSourceFilter.SkillOnly,
                allowFromDot: false,
                damageTypeFilter: null,
                action: ProcActionType.DealDamage,
                actionBuffId: null,
                actionDamageValue: 10,
                actionDamageType: BlazorIdle.Server.Domain.Combat.Damage.DamageType.Physical,
                maxTargets: 3,
                aoeMode: AoEMode.CleaveFull,
                includePrimaryTarget: true,
                splitRemainderToPrimary: true
            );
            context.Procs.RegisterDefinition(aoeProc);
        }

        public override void BuildSkills(BattleContext context, AutoCastEngine engine)
        {
            // 单次触发的瞬发技能（0 资源，超长冷却保证只在 t=0 释放一次）
            engine.AddSkill(new SkillDefinition(
                id: "ping",
                name: "Ping",
                costResourceId: null,
                costAmount: 0,
                cooldownSeconds: 1000,
                priority: 1,
                baseDamage: 0
            ));
        }

        public override void OnSpecialPulse(BattleContext context, SpecialPulseEvent evt)
        {
            // 不加资源，保持纯净
        }

        public override void OnAttackTick(BattleContext context, AttackTickEvent evt)
        {
            // 不产资源，避免触发其他技能
        }
    }

    [Fact]
    public void Proc_DealDamage_AoE_CleaveFull_Hits_Multiple_Targets()
    {
        var runner = new BattleRunner();
        // 将普攻频率设超大，避免普攻伤害干扰；只检查技能触发的 Proc
        var battle = new Battle { CharacterId = Guid.NewGuid(), AttackIntervalSeconds = 999, SpecialIntervalSeconds = 999, StartedAt = 0 };
        var rng = new RngContext(20251004);

        // 三个低抗目标
        var group = new EncounterGroup(new[]
        {
            EnemyRegistry.Resolve("paper"),
            EnemyRegistry.Resolve("paper"),
            EnemyRegistry.Resolve("paper")
        });

        var segs = runner.RunForDuration(battle, 0.2, Profession.Ranger, rng, out _, out _, out _, new TestAoeProcProfession(), null, group);

        var total = segs.Sum(s => s.TotalDamage);
        // 期望：AoE Proc 对 3 个目标各 10 点 → 至少 30 伤
        Assert.True(total >= 30, $"Total damage was {total}, expected at least 30 from AoE proc.");
        var procCount = segs.Sum(s => s.TagCounters.TryGetValue("proc:test_aoe_proc", out var v) ? v : 0);
        Assert.True(procCount >= 1, "Proc should have triggered at least once.");
    }
}