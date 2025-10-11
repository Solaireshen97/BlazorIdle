using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Application.Battles.Simulation;

public sealed class BatchSimulator
{
    private readonly ICharacterRepository _characters;
    private readonly BattleRunner _runner;
    private readonly Domain.Equipment.Services.EquipmentStatsIntegration _equipmentStatsIntegration;

    public BatchSimulator(
        ICharacterRepository characters, 
        BattleRunner runner,
        Domain.Equipment.Services.EquipmentStatsIntegration equipmentStatsIntegration)
    {
        _characters = characters;
        _runner = runner;
        _equipmentStatsIntegration = equipmentStatsIntegration;
    }

    public async Task<SimulateResponse> SimulateAsync(SimulateRequest req, CancellationToken ct = default)
    {
        // 读取角色 + 构造面板（包含装备属性）
        var c = await _characters.GetAsync(req.CharacterId, ct) ?? throw new InvalidOperationException("Character not found");
        var profession = c.Profession;
        var module = ProfessionRegistry.Resolve(profession);

        var attrs = new PrimaryAttributes(c.Strength, c.Agility, c.Intellect, c.Stamina);
        
        // 使用 EquipmentStatsIntegration 构建包含装备加成的完整属性
        var stats = await _equipmentStatsIntegration.BuildStatsWithEquipmentAsync(
            req.CharacterId, profession, attrs);

        var enemyDef = EnemyRegistry.Resolve(req.EnemyId);
        int enemyCount = Math.Max(1, req.EnemyCount);

        // 种子
        ulong baseSeed = req.Seed ?? DeriveSeed(req.CharacterId);

        // 入口参数校正
        double sampleSeconds = Math.Max(1.0, req.SampleSeconds <= 0 ? 20 : req.SampleSeconds);
        double targetSeconds = req.Mode == SimulateMode.Hours ? Math.Max(0.1, req.Value) * 3600.0 : 0.0;
        int targetKills = req.Mode == SimulateMode.Kills ? Math.Max(1, (int)req.Value) : 0;

        // 聚合统计
        var ttkList = new List<double>(capacity: 256);
        double totalSeconds = 0.0;
        long totalDamage = 0;
        int totalKills = 0;
        int runs = 0;

        // 驱动循环（按模式）
        if (req.Mode == SimulateMode.Kills)
        {
            while (totalKills < targetKills)
            {
                runs++;
                ulong seed = RngContext.Hash64(baseSeed ^ (ulong)runs);
                SimOne(sampleSeconds, seed, module, profession, enemyDef, enemyCount, stats, out var dmg, out var dur, out var killed, out var killTime, out var _);
                totalDamage += dmg;
                totalSeconds += dur;
                if (killed)
                {
                    totalKills++;
                    if (killTime.HasValue) ttkList.Add(killTime.Value);
                }

                // 安全断路：若连续若干次没击杀，仍然允许结束（避免永远打不死）
                if (!killed && runs >= targetKills * 10) break;
            }
        }
        else
        {
            while (totalSeconds < targetSeconds)
            {
                runs++;
                // 最后一段可以把样本缩短以精确卡到 targetSeconds
                var remain = targetSeconds - totalSeconds;
                var thisSeconds = Math.Min(sampleSeconds, Math.Max(1.0, remain));

                ulong seed = RngContext.Hash64(baseSeed ^ (ulong)runs);
                SimOne(thisSeconds, seed, module, profession, enemyDef, enemyCount, stats, out var dmg, out var dur, out var killed, out var killTime, out var _);
                totalDamage += dmg;
                totalSeconds += dur;
                if (killed && killTime.HasValue) ttkList.Add(killTime.Value);

                // 冗余保护：长时间仿真也强制上限（例如 1e6 秒）
                if (totalSeconds > 1_000_000) break;
            }
        }

        double avgDps = totalSeconds > 0 ? totalDamage / totalSeconds : 0.0;
        double killsPerHour = totalSeconds > 0 ? (totalKills / totalSeconds) * 3600.0 : 0.0;

        ttkList.Sort();
        var p50 = Percentile(ttkList, 0.50);
        var p90 = Percentile(ttkList, 0.90);
        var p95 = Percentile(ttkList, 0.95);
        var p99 = Percentile(ttkList, 0.99);
        var avgTtk = ttkList.Count > 0 ? ttkList.Average() : (double?)null;

        return new SimulateResponse
        {
            CharacterId = req.CharacterId,
            Profession = profession,
            EnemyId = enemyDef.Id,
            EnemyCount = enemyCount,
            Mode = req.Mode,
            Value = req.Value,
            SampleSeconds = sampleSeconds,
            Runs = runs,

            TotalSimulatedSeconds = totalSeconds,
            TotalDamage = totalDamage,
            TotalKills = totalKills,
            AvgDps = avgDps,
            KillsPerHour = killsPerHour,

            AvgTtk = avgTtk,
            TtkP50 = p50,
            TtkP90 = p90,
            TtkP95 = p95,
            TtkP99 = p99
        };
    }

    private void SimOne(
        double seconds,
        ulong seed,
        IProfessionModule module,
        Profession profession,
        EnemyDefinition enemyDef,
        int enemyCount,
        CharacterStats stats,
        out int totalDamage,
        out double durationSeconds,
        out bool killed,
        out double? killTime,
        out int overkill)
    {
        // 构造 EncounterGroup
        enemyCount = Math.Max(1, enemyCount);
        var groupDefs = Enumerable.Range(0, enemyCount).Select(_ => enemyDef).ToList();
        var encounterGroup = new EncounterGroup(groupDefs);

        var battleDomain = new Battle
        {
            CharacterId = Guid.Empty, // 批量模拟不入库
            AttackIntervalSeconds = module.BaseAttackInterval,
            SpecialIntervalSeconds = module.BaseSpecialInterval,
            StartedAt = 0
        };

        var rng = new RngContext(seed);

        var segments = _runner.RunForDuration(
            battle: battleDomain,
            durationSeconds: seconds,
            profession: profession,
            rng: rng,
            out killed,
            out killTime,
            out overkill,
            module: module,
            encounter: null,
            encounterGroup: encounterGroup,
            stats: stats
        );

        totalDamage = segments.Sum(s => s.TotalDamage);
        durationSeconds = killTime ?? seconds;
    }

    private static double? Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return null;
        if (p <= 0) return sorted[0];
        if (p >= 1) return sorted[^1];
        double i = (sorted.Count - 1) * p;
        int lo = (int)Math.Floor(i);
        int hi = (int)Math.Ceiling(i);
        if (lo == hi) return sorted[lo];
        double frac = i - lo;
        return sorted[lo] * (1 - frac) + sorted[hi] * frac;
    }

    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}

// DTO（服务端内部）
public enum SimulateMode { Kills, Hours }

public sealed class SimulateRequest
{
    public Guid CharacterId { get; set; }
    public string EnemyId { get; set; } = "dummy";
    public int EnemyCount { get; set; } = 1;

    public SimulateMode Mode { get; set; } = SimulateMode.Kills;
    public double Value { get; set; } = 10;          // Kills 模式=击杀数；Hours 模式=小时数

    public double SampleSeconds { get; set; } = 20;  // 单次样本时长（两种模式都用）
    public ulong? Seed { get; set; }                 // 可选：基准种子（会派生到每次样本）
}

public sealed class SimulateResponse
{
    public Guid CharacterId { get; set; }
    public Profession Profession { get; set; }
    public string EnemyId { get; set; } = "dummy";
    public int EnemyCount { get; set; }

    public SimulateMode Mode { get; set; }
    public double Value { get; set; }
    public double SampleSeconds { get; set; }
    public int Runs { get; set; }

    public double TotalSimulatedSeconds { get; set; }
    public long TotalDamage { get; set; }
    public int TotalKills { get; set; }

    public double AvgDps { get; set; }
    public double KillsPerHour { get; set; }

    public double? AvgTtk { get; set; }
    public double? TtkP50 { get; set; }
    public double? TtkP90 { get; set; }
    public double? TtkP95 { get; set; }
    public double? TtkP99 { get; set; }
}