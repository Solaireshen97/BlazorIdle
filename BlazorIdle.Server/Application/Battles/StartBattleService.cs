using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Records;
using System.Text.Json;

namespace BlazorIdle.Server.Application.Battles;

public class StartBattleService
{
    private readonly ICharacterRepository _characters;
    private readonly IBattleRepository _battles;
    private readonly BattleRunner _runner;

    public StartBattleService(ICharacterRepository characters, IBattleRepository battles, BattleRunner runner)
    {
        _characters = characters;
        _battles = battles;
        _runner = runner;
    }

    // 新增: mode/dungeonId，mode: duration(默认) | continuous | dungeon | dungeonloop
    public async Task<Guid> StartAsync(
        Guid characterId,
        double simulateSeconds = 15,
        ulong? seed = null,
        string? enemyId = null,
        int enemyCount = 1,
        string? mode = null,
        string? dungeonId = null,
        CancellationToken ct = default)
    {
        var c = await _characters.GetAsync(characterId, ct);
        if (c is null) throw new InvalidOperationException("Character not found");

        var module = ProfessionRegistry.Resolve(c.Profession);

        var enemyDef = EnemyRegistry.Resolve(enemyId);
        enemyCount = Math.Max(1, enemyCount);

        var battleDomain = new Battle
        {
            CharacterId = characterId,
            AttackIntervalSeconds = module.BaseAttackInterval,
            SpecialIntervalSeconds = module.BaseSpecialInterval,
            StartedAt = 0
        };

        // 职业基础 + 主属性转换
        var baseStats = ProfessionBaseStatsRegistry.Resolve(c.Profession);
        var attrs = new PrimaryAttributes(c.Strength, c.Agility, c.Intellect, c.Stamina);
        var derived = StatsBuilder.BuildDerived(c.Profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        ulong finalSeed = seed ?? DeriveSeed(characterId);

        // 解析 mode
        var m = (mode ?? "duration").Trim().ToLowerInvariant();

        List<CombatSegment> segments;
        bool killed;
        double? killTime;
        int overkill;
        long seedIndexStart;
        long seedIndexEnd;

        if (m == "continuous" || m == "dungeon" || m == "dungeonloop")
        {
            // 复用 Step 的 RunningBattle，同步快进到 simulateSeconds
            var stepMode = m switch
            {
                "continuous" => StepBattleMode.Continuous,
                "dungeon" => StepBattleMode.DungeonSingle,
                "dungeonloop" => StepBattleMode.DungeonLoop,
                _ => StepBattleMode.Duration
            };

            var rng = new RngContext(finalSeed);
            seedIndexStart = rng.Index;

            var rb = new RunningBattle(
                id: battleDomain.Id,
                characterId: characterId,
                profession: c.Profession,
                seed: finalSeed,
                targetSeconds: simulateSeconds,
                enemyDef: enemyDef,
                enemyCount: enemyCount,
                stats: stats,
                mode: stepMode,
                dungeonId: dungeonId
            );

            // 快进至目标秒数（非持续模式也会完成提前结束）
            rb.FastForwardTo(simulateSeconds);

            segments = rb.Segments.ToList();
            killed = rb.Killed;
            killTime = rb.KillTime;
            overkill = rb.Overkill;
            seedIndexEnd = rb.SeedIndexEnd;
        }
        else
        {
            // 原有一次性时长模式
            var rng = new RngContext(finalSeed);
            seedIndexStart = rng.Index;

            // 构造 EncounterGroup（单波）
            var groupDefs = Enumerable.Range(0, enemyCount).Select(_ => enemyDef).ToList();
            var encounterGroup = new EncounterGroup(groupDefs);

            segments = _runner.RunForDuration(
                battleDomain, simulateSeconds, c.Profession, rng,
                out killed, out killTime, out overkill,
                module: module,
                encounter: null,
                encounterGroup: encounterGroup,
                stats: stats
            ).ToList();

            seedIndexEnd = rng.Index;
        }

        var totalDamage = segments.Sum(s => s.TotalDamage);

        var record = new BattleRecord
        {
            Id = battleDomain.Id,
            CharacterId = characterId,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            TotalDamage = totalDamage,
            DurationSeconds = (killTime.HasValue ? killTime.Value : simulateSeconds),
            AttackIntervalSeconds = battleDomain.AttackIntervalSeconds,
            SpecialIntervalSeconds = battleDomain.SpecialIntervalSeconds,
            Seed = finalSeed.ToString(),
            SeedIndexStart = seedIndexStart,
            SeedIndexEnd = seedIndexEnd,

            EnemyId = enemyDef.Id,
            EnemyName = enemyDef.Name,
            EnemyLevel = enemyDef.Level,
            EnemyMaxHp = enemyDef.MaxHp,
            EnemyArmor = enemyDef.Armor,
            EnemyMagicResist = enemyDef.MagicResist,
            Killed = killed,
            KillTimeSeconds = killTime,
            OverkillDamage = overkill,

            Segments = segments.Select(s => new BattleSegmentRecord
            {
                Id = Guid.NewGuid(),
                BattleId = battleDomain.Id,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                EventCount = s.EventCount,
                TotalDamage = s.TotalDamage,
                DamageBySourceJson = JsonSerializer.Serialize(s.DamageBySource),
                TagCountersJson = JsonSerializer.Serialize(s.TagCounters),
                ResourceFlowJson = JsonSerializer.Serialize(s.ResourceFlow),
                DamageByTypeJson = JsonSerializer.Serialize(s.DamageByType),
                RngIndexStart = s.RngIndexStart,
                RngIndexEnd = s.RngIndexEnd
            }).ToList()
        };

        await _battles.AddAsync(record, ct);
        return record.Id;
    }

    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}