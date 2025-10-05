using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Application.Battles;

/// <summary>
/// 同步版战斗驱动：基于 BattleEngine 推进到目标时长并返回段聚合。
/// 支持：
/// - 传统单波（不刷新）
/// - 持续/地城（通过 IEncounterProvider 实现刷新、波次、循环）
/// </summary>
public class BattleRunner
{
    public IReadOnlyList<CombatSegment> RunForDuration(
        Battle battle,
        double durationSeconds,
        Profession profession,
        RngContext rng,
        out bool killed,
        out double? killTime,
        out int overkill,
        IProfessionModule? module = null,
        Encounter? encounter = null,                  // 兼容旧签名
        EncounterGroup? encounterGroup = null,        // 兼容旧签名
        CharacterStats? stats = null,
        IEncounterProvider? provider = null)          // 新增：持续/地城
    {
        var engine =
            provider is not null
            ? new BattleEngine(
                battleId: battle.Id,
                characterId: battle.CharacterId,
                profession: profession,
                stats: stats ?? new CharacterStats(),
                rng: rng,
                provider: provider,
                module: module)
            : new BattleEngine(
                battleId: battle.Id,
                characterId: battle.CharacterId,
                profession: profession,
                stats: stats ?? new CharacterStats(),
                rng: rng,
                enemyDef: (encounterGroup?.All?.FirstOrDefault()?.Enemy)
                          ?? (encounter is not null ? encounter.Enemy : EnemyRegistry.Resolve("dummy")),
                enemyCount: encounterGroup?.All?.Count ?? (encounter is null ? 1 : 1),
                module: module);

        // 推进到目标时长（持续/地城将按 provider 内的 delay/波次自动刷新）
        engine.AdvanceUntil(durationSeconds);

        killed = engine.Killed;
        killTime = engine.KillTime;
        overkill = engine.Overkill;

        // 结束时间/间隔回写（保持原行为）
        battle.Finish(engine.Battle.EndedAt ?? engine.Clock.CurrentTime);
        return engine.Segments;
    }
}