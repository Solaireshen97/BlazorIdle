using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Application.Monitoring;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Application.Battles;

/// <summary>
/// 统一的战斗模拟组件，封装 BattleEngine 的创建和配置逻辑。
/// 支持同步执行（BattleRunner）、异步执行（RunningBattle）和离线结算（OfflineSettlement）复用。
/// Phase 6: 集成MetricsCollectorService，记录战斗系统指标
/// </summary>
public class BattleSimulator
{
    private readonly IMetricsCollectorService? _metrics;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="metrics">Phase 6: 性能监控指标收集服务（可选）</param>
    public BattleSimulator(IMetricsCollectorService? metrics = null)
    {
        _metrics = metrics;
    }
    /// <summary>
    /// 战斗模拟结果
    /// </summary>
    public sealed class SimulationResult
    {
        public IReadOnlyList<CombatSegment> Segments { get; init; } = Array.Empty<CombatSegment>();
        public bool Killed { get; init; }
        public double? KillTime { get; init; }
        public int Overkill { get; init; }
        public long SeedIndexStart { get; init; }
        public long SeedIndexEnd { get; init; }
        public Battle Battle { get; init; } = null!;
    }

    /// <summary>
    /// 战斗配置参数
    /// </summary>
    public sealed class BattleConfig
    {
        public Guid BattleId { get; init; }
        public Guid CharacterId { get; init; }
        public Profession Profession { get; init; }
        public CharacterStats Stats { get; init; } = new();
        public ulong Seed { get; init; }
        public RngContext? Rng { get; init; }  // 可选：如果已有 RngContext 则使用，否则用 Seed 创建
        public EnemyDefinition EnemyDef { get; init; } = null!;
        public int EnemyCount { get; init; } = 1;
        public string Mode { get; init; } = "duration";
        public string? DungeonId { get; init; }
        public double? ContinuousRespawnDelaySeconds { get; init; }
        public double? DungeonWaveDelaySeconds { get; init; }
        public double? DungeonRunDelaySeconds { get; init; }
        /// <summary>Phase 5: 可选的攻击间隔覆盖（用于武器类型影响攻击速度）</summary>
        public double? AttackIntervalSeconds { get; init; }
        public IProfessionModule? Module { get; init; }
        public Encounter? Encounter { get; init; }
        public EncounterGroup? EncounterGroup { get; init; }
        public IEncounterProvider? Provider { get; init; }
    }

    /// <summary>
    /// 创建并执行战斗模拟（同步方式，一次性运行到指定时长）
    /// </summary>
    public SimulationResult RunForDuration(BattleConfig config, double durationSeconds)
    {
        var rng = config.Rng ?? new RngContext(config.Seed);
        var seedIndexStart = rng.Index;

        var module = config.Module ?? ProfessionRegistry.Resolve(config.Profession);
        var battle = new Battle
        {
            Id = config.BattleId,
            CharacterId = config.CharacterId,
            // Phase 5: 使用配置中的攻击间隔（如果提供），否则使用职业基础间隔
            AttackIntervalSeconds = config.AttackIntervalSeconds ?? module.BaseAttackInterval,
            SpecialIntervalSeconds = module.BaseSpecialInterval,
            StartedAt = 0
        };

        var engine = CreateBattleEngine(config, battle, rng);
        engine.AdvanceUntil(durationSeconds);

        battle.Finish(engine.Battle.EndedAt ?? engine.Clock.CurrentTime);

        // Phase 6: 记录战斗系统指标
        var totalEventCount = engine.Segments.Sum(s => s.EventCount);
        var battleDuration = engine.Clock.CurrentTime;
        _metrics?.RecordBattleDuration(config.BattleId, battleDuration, totalEventCount);

        // 记录战斗伤害统计（如果有数据）
        if (engine.Segments.Any() && engine.Context.Encounter != null)
        {
            var totalDamage = engine.Segments.Sum(s => 
                s.DamageBySource?.Values.Sum() ?? 0);
            var averageDps = battleDuration > 0 ? totalDamage / battleDuration : 0;
            _metrics?.RecordBattleDamage(config.BattleId, totalDamage, averageDps);
        }

        return new SimulationResult
        {
            Segments = engine.Segments,
            Killed = engine.Killed,
            KillTime = engine.KillTime,
            Overkill = engine.Overkill,
            SeedIndexStart = seedIndexStart,
            SeedIndexEnd = engine.SeedIndexEnd,
            Battle = battle
        };
    }

    /// <summary>
    /// 创建 RunningBattle 实例（用于异步/步进式战斗）
    /// </summary>
    public RunningBattle CreateRunningBattle(BattleConfig config, double targetDurationSeconds)
    {
        var stepMode = ParseStepBattleMode(config.Mode);

        return new RunningBattle(
            id: config.BattleId,
            characterId: config.CharacterId,
            profession: config.Profession,
            seed: config.Seed,
            targetSeconds: targetDurationSeconds,
            enemyDef: config.EnemyDef,
            enemyCount: config.EnemyCount,
            stats: config.Stats,
            mode: stepMode,
            dungeonId: config.DungeonId,
            continuousRespawnDelaySeconds: config.ContinuousRespawnDelaySeconds,
            dungeonWaveDelaySeconds: config.DungeonWaveDelaySeconds,
            dungeonRunDelaySeconds: config.DungeonRunDelaySeconds,
            module: config.Module
        );
    }

    /// <summary>
    /// 创建 BattleEngine 实例（内部辅助方法）
    /// </summary>
    private BattleEngine CreateBattleEngine(BattleConfig config, Battle battle, RngContext rng)
    {
        // 构造 meta
        var meta = CreateBattleMeta(config);

        // 决定使用 provider 还是简单的 enemyDef
        if (config.Provider is not null)
        {
            return new BattleEngine(
                battleId: battle.Id,
                characterId: config.CharacterId,
                profession: config.Profession,
                stats: config.Stats,
                rng: rng,
                provider: config.Provider,
                module: config.Module,
                meta: meta
            );
        }

        // 如果配置了 mode 需要创建 provider
        var provider = CreateEncounterProvider(config);
        if (provider is not null)
        {
            return new BattleEngine(
                battleId: battle.Id,
                characterId: config.CharacterId,
                profession: config.Profession,
                stats: config.Stats,
                rng: rng,
                provider: provider,
                module: config.Module,
                meta: meta
            );
        }

        // 使用 encounterGroup 或简单的 enemyDef
        var enemyDef = config.EncounterGroup?.All?.FirstOrDefault()?.Enemy
                       ?? config.Encounter?.Enemy
                       ?? config.EnemyDef;
        var enemyCount = config.EncounterGroup?.All?.Count
                         ?? (config.Encounter is null ? config.EnemyCount : 1);

        return new BattleEngine(
            battleId: battle.Id,
            characterId: config.CharacterId,
            profession: config.Profession,
            stats: config.Stats,
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: enemyCount,
            module: config.Module,
            meta: meta
        );
    }

    /// <summary>
    /// 根据配置创建 IEncounterProvider
    /// </summary>
    private IEncounterProvider? CreateEncounterProvider(BattleConfig config)
    {
        var mode = config.Mode.Trim().ToLowerInvariant();

        return mode switch
        {
            "continuous" => new ContinuousEncounterProvider(
                config.EnemyDef,
                config.EnemyCount,
                respawnDelaySeconds: config.ContinuousRespawnDelaySeconds ?? 3.0
            ),
            "dungeon" or "dungeonsingle" => new DungeonEncounterProvider(
                DungeonRegistry.Resolve(config.DungeonId ?? "intro_cave"),
                loop: false,
                waveDelayOverride: config.DungeonWaveDelaySeconds,
                runDelayOverride: config.DungeonRunDelaySeconds
            ),
            "dungeonloop" => new DungeonEncounterProvider(
                DungeonRegistry.Resolve(config.DungeonId ?? "intro_cave"),
                loop: true,
                waveDelayOverride: config.DungeonWaveDelaySeconds,
                runDelayOverride: config.DungeonRunDelaySeconds
            ),
            _ => null // "duration" 模式不需要 provider
        };
    }

    /// <summary>
    /// 创建 BattleMeta
    /// </summary>
    private BattleMeta CreateBattleMeta(BattleConfig config)
    {
        var mode = config.Mode.Trim().ToLowerInvariant();
        var modeTag = mode switch
        {
            "continuous" => "continuous",
            "dungeon" or "dungeonsingle" => "dungeonsingle",
            "dungeonloop" => "dungeonloop",
            _ => "duration"
        };

        string? dungeonId = null;
        if (mode.Contains("dungeon"))
        {
            dungeonId = config.DungeonId ?? "intro_cave";
        }

        var enemyId = config.EncounterGroup?.All?.FirstOrDefault()?.Enemy?.Id
                      ?? config.Encounter?.Enemy?.Id
                      ?? config.EnemyDef?.Id
                      ?? "dummy";

        var enemyCount = config.EncounterGroup?.All?.Count ?? config.EnemyCount;

        return new BattleMeta
        {
            ModeTag = modeTag,
            EnemyId = enemyId,
            EnemyCount = enemyCount,
            DungeonId = dungeonId
        };
    }

    /// <summary>
    /// 将字符串模式转换为 StepBattleMode 枚举
    /// </summary>
    private StepBattleMode ParseStepBattleMode(string mode)
    {
        return mode.Trim().ToLowerInvariant() switch
        {
            "continuous" => StepBattleMode.Continuous,
            "dungeon" or "dungeonsingle" => StepBattleMode.DungeonSingle,
            "dungeonloop" => StepBattleMode.DungeonLoop,
            _ => StepBattleMode.Duration
        };
    }
}
