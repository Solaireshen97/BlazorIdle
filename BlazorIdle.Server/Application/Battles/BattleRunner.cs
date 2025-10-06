using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Application.Battles;

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
        Encounter? encounter = null,
        EncounterGroup? encounterGroup = null,
        CharacterStats? stats = null,
        IEncounterProvider? provider = null)
    {
        // 组装 meta：provider 区分 dungeon/continuous，缺省即 duration
        string modeTag = "duration";
        string enemyId = (encounterGroup?.All?.FirstOrDefault()?.Enemy?.Id)
                         ?? (encounter?.Enemy?.Id ?? "dummy");
        int enemyCount = encounterGroup?.All?.Count ?? 1;
        string? dungeonId = null;

        if (provider is DungeonEncounterProvider dprov)
        {
            modeTag = "dungeon";
            dungeonId = dprov.DungeonId; // 见下方 provider 暴露属性
        }
        else if (provider is ContinuousEncounterProvider)
        {
            modeTag = "continuous";
        }

        var meta = new BattleMeta
        {
            ModeTag = modeTag,
            EnemyId = enemyId,
            EnemyCount = enemyCount,
            DungeonId = dungeonId
        };

        var engine =
            provider is not null
            ? new BattleEngine(
                battleId: battle.Id,
                characterId: battle.CharacterId,
                profession: profession,
                stats: stats ?? new CharacterStats(),
                rng: rng,
                provider: provider,
                module: module,
                meta: meta)
            : new BattleEngine(
                battleId: battle.Id,
                characterId: battle.CharacterId,
                profession: profession,
                stats: stats ?? new CharacterStats(),
                rng: rng,
                enemyDef: (encounterGroup?.All?.FirstOrDefault()?.Enemy)
                          ?? (encounter is not null ? encounter.Enemy : EnemyRegistry.Resolve("dummy")),
                enemyCount: encounterGroup?.All?.Count ?? (encounter is null ? 1 : 1),
                module: module,
                meta: meta);

        engine.AdvanceUntil(durationSeconds);

        killed = engine.Killed;
        killTime = engine.KillTime;
        overkill = engine.Overkill;

        battle.Finish(engine.Battle.EndedAt ?? engine.Clock.CurrentTime);
        return engine.Segments;
    }
}