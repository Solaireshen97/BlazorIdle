using BlazorIdle.Server.Application.Abstractions;
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

    // 新增 enemyCount 参数（默认 1），用于创建多目标遭遇组；不改数据库
    public async Task<Guid> StartAsync(Guid characterId, double simulateSeconds = 15, ulong? seed = null, string? enemyId = null, int enemyCount = 1, CancellationToken ct = default)
    {
        var c = await _characters.GetAsync(characterId, ct);
        if (c is null) throw new InvalidOperationException("Character not found");

        var module = ProfessionRegistry.Resolve(c.Profession);

        var enemyDef = EnemyRegistry.Resolve(enemyId);
        enemyCount = Math.Max(1, enemyCount);
        var groupDefs = Enumerable.Range(0, enemyCount).Select(_ => enemyDef).ToList();
        var encounterGroup = new EncounterGroup(groupDefs);

        var battleDomain = new Battle
        {
            CharacterId = characterId,
            AttackIntervalSeconds = module.BaseAttackInterval,
            SpecialIntervalSeconds = module.BaseSpecialInterval,
            StartedAt = 0
        };

        // 职业基础 Stats：不含主属性/装备转换（Haste=0，急速仅由 Buff/专门属性影响）
        var stats = ProfessionBaseStatsRegistry.Resolve(c.Profession);

        ulong finalSeed = seed ?? DeriveSeed(characterId);
        var rng = new RngContext(finalSeed);
        long seedIndexStart = rng.Index;

        var segments = _runner.RunForDuration(
            battleDomain, simulateSeconds, c.Profession, rng,
            out var killed, out var killTime, out var overkill,
            module: module,
            encounter: null,               // 主目标由组内的 PrimaryAlive 决定
            encounterGroup: encounterGroup, // 多目标组
            stats: stats                    // 注入职业基础面板
        );

        long seedIndexEnd = rng.Index;
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

            // 敌人与击杀信息：沿用单目标（主目标）信息，不做 DB 变更
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
                DamageByTypeJson = JsonSerializer.Serialize(s.DamageByType)
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