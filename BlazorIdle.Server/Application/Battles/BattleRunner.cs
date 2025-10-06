using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Application.Battles;

public class BattleRunner
{
    private readonly BattleSimulator _simulator;

    public BattleRunner(BattleSimulator simulator)
    {
        _simulator = simulator;
    }

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
        // 决定模式标签
        string mode = "duration";
        string? dungeonId = null;

        if (provider is DungeonEncounterProvider dprov)
        {
            mode = "dungeon";
            dungeonId = dprov.DungeonId;
        }
        else if (provider is ContinuousEncounterProvider)
        {
            mode = "continuous";
        }

        // 使用 BattleSimulator 统一创建和执行
        var config = new BattleSimulator.BattleConfig
        {
            BattleId = battle.Id,
            CharacterId = battle.CharacterId,
            Profession = profession,
            Stats = stats ?? new CharacterStats(),
            Rng = rng,
            EnemyDef = (encounterGroup?.All?.FirstOrDefault()?.Enemy)
                       ?? (encounter is not null ? encounter.Enemy : EnemyRegistry.Resolve("dummy")),
            EnemyCount = encounterGroup?.All?.Count ?? (encounter is null ? 1 : 1),
            Mode = mode,
            DungeonId = dungeonId,
            Module = module,
            Encounter = encounter,
            EncounterGroup = encounterGroup,
            Provider = provider
        };

        var result = _simulator.RunForDuration(config, durationSeconds);

        killed = result.Killed;
        killTime = result.KillTime;
        overkill = result.Overkill;

        // 使用 simulator 返回的 battle 状态更新传入的 battle
        battle.Finish(result.Battle.EndedAt ?? durationSeconds);
        return result.Segments;
    }
}
