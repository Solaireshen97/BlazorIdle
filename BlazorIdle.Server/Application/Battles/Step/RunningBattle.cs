using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System;
using System.Collections.Generic;

namespace BlazorIdle.Server.Application.Battles.Step;

public enum StepBattleMode
{
    Duration = 0,
    Continuous = 1,
    DungeonSingle = 2,
    DungeonLoop = 3
}

public sealed class RunningBattle
{
    public Guid Id { get; }
    public Guid CharacterId { get; }
    public Profession Profession { get; }
    public double TargetDurationSeconds { get; }
    public string EnemyId { get; }
    public int EnemyCount { get; }

    public StepBattleMode Mode { get; }
    public string? DungeonId { get; }

    public BattleEngine Engine { get; }

    // 兼容暴露（供 StepBattleCoordinator 读取）
    public Battle Battle => Engine.Battle;
    public IGameClock Clock => Engine.Clock;
    public IEventScheduler Scheduler => Engine.Scheduler;
    public SegmentCollector Collector => Engine.Collector;
    public BattleContext Context => Engine.Context;
    public List<CombatSegment> Segments => Engine.Segments;

    // 新增：向后兼容属性，代理到 Engine
    public int WaveIndex => Engine.WaveIndex;
    public int RunCount => Engine.RunCount;

    public bool Completed { get; internal set; }
    public bool Killed => Engine.Killed;
    public double? KillTime => Engine.KillTime;
    public int Overkill => Engine.Overkill;

    public ulong Seed { get; }
    public long SeedIndexStart { get; }
    public long SeedIndexEnd => Engine.SeedIndexEnd;

    public bool Persisted { get; internal set; }
    public Guid? PersistedBattleId { get; internal set; }

    public DateTime StartedWallUtc { get; }
    private DateTime _lastAdvanceWallUtc;
    public double SimSpeed { get; } = 1.0;

    public RunningBattle(
        Guid id,
        Guid characterId,
        Profession profession,
        ulong seed,
        double targetSeconds,
        EnemyDefinition enemyDef,
        int enemyCount,
        CharacterStats stats,
        StepBattleMode mode = StepBattleMode.Duration,
        string? dungeonId = null,
        double? continuousRespawnDelaySeconds = null,
        double? dungeonWaveDelaySeconds = null,
        double? dungeonRunDelaySeconds = null,
        IProfessionModule? module = null)
    {
        Id = id;
        CharacterId = characterId;
        Profession = profession;
        TargetDurationSeconds = targetSeconds;
        EnemyId = enemyDef.Id;
        EnemyCount = Math.Max(1, enemyCount);
        Mode = mode;
        DungeonId = dungeonId;
        Seed = seed;

        var rng = new RngContext(seed);
        SeedIndexStart = rng.Index;

        IEncounterProvider? provider = null;
        if (mode == StepBattleMode.DungeonSingle || mode == StepBattleMode.DungeonLoop)
        {
            var dungeon = DungeonRegistry.Resolve(dungeonId ?? "intro_cave");
            provider = new DungeonEncounterProvider(
                dungeon,
                loop: mode == StepBattleMode.DungeonLoop,
                waveDelayOverride: dungeonWaveDelaySeconds,
                runDelayOverride: dungeonRunDelaySeconds
            );
        }
        else if (mode == StepBattleMode.Continuous)
        {
            provider = new ContinuousEncounterProvider(enemyDef, EnemyCount, respawnDelaySeconds: continuousRespawnDelaySeconds ?? 3.0);
        }

        Engine =
            provider is not null
            ? new BattleEngine(
                battleId: id,
                characterId: characterId,
                profession: profession,
                stats: stats,
                rng: rng,
                provider: provider,
                module: module)
            : new BattleEngine(
                battleId: id,
                characterId: characterId,
                profession: profession,
                stats: stats,
                rng: rng,
                enemyDef: enemyDef,
                enemyCount: EnemyCount,
                module: module);

        if (mode == StepBattleMode.DungeonSingle || mode == StepBattleMode.DungeonLoop)
        {
            Engine.Collector.OnTag($"ctx.dungeonId.{(dungeonId ?? "intro_cave")}", 1);
        }
        StartedWallUtc = DateTime.UtcNow;
        _lastAdvanceWallUtc = StartedWallUtc;
    }

    public void Advance(int maxEvents = 2000, double maxSimSecondsSlice = 0.25)
    {
        if (Completed) return;

        var wallNow = DateTime.UtcNow;
        var wallDelta = (wallNow - _lastAdvanceWallUtc).TotalSeconds;
        if (wallDelta <= 0.0005) return;

        var allowedDelta = Math.Min(wallDelta * SimSpeed, Math.Max(0.001, maxSimSecondsSlice));
        var sliceEnd = (Mode == StepBattleMode.Duration)
            ? Math.Min(TargetDurationSeconds, Clock.CurrentTime + allowedDelta)
            : (Clock.CurrentTime + allowedDelta);

        Engine.AdvanceTo(sliceEnd, maxEvents);

        if (Mode == StepBattleMode.Duration && (Clock.CurrentTime >= TargetDurationSeconds))
        {
            Completed = true;
            Battle.Finish(Clock.CurrentTime);
        }

        _lastAdvanceWallUtc = wallNow;
    }

    public void ForceStopAndSeal()
    {
        if (Completed) return;
        Engine.AdvanceUntil(Clock.CurrentTime);
        Completed = true;
        Battle.Finish(Clock.CurrentTime);
    }

    public void FastForwardTo(double targetSimSeconds)
    {
        if (Completed) return;
        targetSimSeconds = Math.Max(0, (Mode == StepBattleMode.Duration
            ? Math.Min(TargetDurationSeconds, targetSimSeconds)
            : targetSimSeconds));

        while (Clock.CurrentTime + 1e-6 < targetSimSeconds && !Completed)
        {
            _lastAdvanceWallUtc = _lastAdvanceWallUtc.AddSeconds(-3600);
            var remain = targetSimSeconds - Clock.CurrentTime;
            var slice = Math.Min(5.0, Math.Max(0.001, remain));
            Advance(maxEvents: 1_000_000, maxSimSecondsSlice: slice);
            if (Scheduler.Count == 0) break;
        }
    }
}