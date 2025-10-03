using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Records;

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

    public async Task<Guid> StartAsync(Guid characterId, double simulateSeconds = 15, CancellationToken ct = default)
    {
        var c = await _characters.GetAsync(characterId, ct);
        if (c is null) throw new InvalidOperationException("Character not found");

        var module = ProfessionRegistry.Resolve(c.Profession);

        var battleDomain = new Battle
        {
            CharacterId = characterId,
            AttackIntervalSeconds = module.BaseAttackInterval,
            SpecialIntervalSeconds = module.BaseSpecialInterval,
            StartedAt = 0
        };

        var segments = _runner.RunForDuration(battleDomain, simulateSeconds, c.Profession, module);
        var totalDamage = segments.Sum(s => s.TotalDamage);

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
            Segments = segments.Select(s => new BattleSegmentRecord
            {
                Id = Guid.NewGuid(),
                BattleId = battleDomain.Id,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                EventCount = s.EventCount,
                TotalDamage = s.TotalDamage,
                DamageBySourceJson = System.Text.Json.JsonSerializer.Serialize(s.DamageBySource),
                TagCountersJson = System.Text.Json.JsonSerializer.Serialize(s.TagCounters),
                ResourceFlowJson = System.Text.Json.JsonSerializer.Serialize(s.ResourceFlow)
            }).ToList()
        };

        await _battles.AddAsync(record, ct);
        return record.Id;
    }
}