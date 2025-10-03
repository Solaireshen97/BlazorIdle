using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
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

    public async Task<Guid> StartAsync(Guid characterId, double simulateSeconds = 15, ulong? seed = null, CancellationToken ct = default)
    {
        var c = await _characters.GetAsync(characterId, ct);
        if (c is null) throw new InvalidOperationException("Character not found");

        var module = ProfessionRegistry.Resolve(c.Profession);

        // 构建 Battle（由职业提供基础间隔）
        var battleDomain = new Battle
        {
            CharacterId = characterId,
            AttackIntervalSeconds = module.BaseAttackInterval,
            SpecialIntervalSeconds = module.BaseSpecialInterval,
            StartedAt = 0
        };

        // 准备 RNG
        ulong finalSeed = seed ?? DeriveSeed(characterId);
        var rng = new RngContext(finalSeed);
        long seedIndexStart = rng.Index;

        // 执行
        var segments = _runner.RunForDuration(battleDomain, simulateSeconds, c.Profession, rng, module);
        long seedIndexEnd = rng.Index;
        var totalDamage = segments.Sum(s => s.TotalDamage);

        // 映射持久化
        var record = new BattleRecord
        {
            Id = battleDomain.Id,
            CharacterId = characterId,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            TotalDamage = totalDamage,
            DurationSeconds = simulateSeconds,
            AttackIntervalSeconds = battleDomain.AttackIntervalSeconds,
            SpecialIntervalSeconds = battleDomain.SpecialIntervalSeconds,
            Seed = finalSeed.ToString(),              // 新增
            SeedIndexStart = seedIndexStart,          // 新增
            SeedIndexEnd = seedIndexEnd,              // 新增
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
                ResourceFlowJson = JsonSerializer.Serialize(s.ResourceFlow)
            }).ToList()
        };

        await _battles.AddAsync(record, ct);
        return record.Id;
    }

    private static ulong DeriveSeed(Guid characterId)
    {
        // 由角色 Id 与当前时间混合
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4); // 打散几步
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}