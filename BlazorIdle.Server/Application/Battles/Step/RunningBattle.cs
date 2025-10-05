using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System;
using System.Linq;

namespace BlazorIdle.Server.Application.Battles.Step;

/// <summary>
/// 异步 Step 战斗包装器：仅负责墙钟切片与目标时长，战斗核心由 BattleEngine 驱动。
/// </summary>
public sealed class RunningBattle
{
    public Guid Id { get; }
    public Guid CharacterId { get; }
    public Profession Profession { get; }
    public double TargetDurationSeconds { get; }
    public string EnemyId { get; }
    public int EnemyCount { get; }

    public BattleEngine Engine { get; }

    // 为保持外部兼容，继续暴露以下快捷引用
    public Battle Battle => Engine.Battle;
    public IGameClock Clock => Engine.Clock;
    public IEventScheduler Scheduler => Engine.Scheduler;
    public SegmentCollector Collector => Engine.Collector;
    public BattleContext Context => Engine.Context;
    public System.Collections.Generic.List<CombatSegment> Segments => Engine.Segments;

    public bool Completed { get; internal set; }
    public bool Killed { get; private set; }
    public double? KillTime { get; private set; }
    public int Overkill { get; private set; }

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
        IProfessionModule? module = null)
    {
        Id = id;
        CharacterId = characterId;
        Profession = profession;
        TargetDurationSeconds = targetSeconds;
        EnemyId = enemyDef.Id;
        EnemyCount = Math.Max(1, enemyCount);
        Seed = seed;

        var rng = new RngContext(seed);
        SeedIndexStart = rng.Index;

        Engine = new BattleEngine(
            battleId: id,
            characterId: characterId,
            profession: profession,
            stats: stats,
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: EnemyCount,
            module: module
        );

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
        var sliceEnd = Math.Min(TargetDurationSeconds, Clock.CurrentTime + allowedDelta);

        // 核心推进交给 Engine（含 RNG 段记录/flush/死亡判定）
        Engine.AdvanceTo(sliceEnd, maxEvents);

        if (Clock.CurrentTime >= TargetDurationSeconds || Engine.Completed)
        {
            // 封盘（Engine 会完成最终段 flush）
            Engine.FinalizeNow();

            Killed = Engine.Killed;
            KillTime = Engine.KillTime;
            Overkill = Engine.Overkill;
            Completed = true;
        }

        _lastAdvanceWallUtc = wallNow;
    }

    // 手动终止：不再推进模拟时间，直接封盘
    public void ForceStopAndSeal()
    {
        if (Completed) return;
        Engine.FinalizeNow();

        Killed = Engine.Killed;
        KillTime = Engine.KillTime;
        Overkill = Engine.Overkill;
        Completed = true;
    }

    // 恢复追帧：用大切片快速推进到目标模拟秒
    public void FastForwardTo(double targetSimSeconds)
    {
        if (Completed) return;
        targetSimSeconds = Math.Max(0, Math.Min(TargetDurationSeconds, targetSimSeconds));

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