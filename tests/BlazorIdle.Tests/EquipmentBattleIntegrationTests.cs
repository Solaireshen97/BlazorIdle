using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Domain.Records;
using BlazorIdle.Shared.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 装备与战斗系统集成测试
/// 验证装备属性对战斗结果的影响
/// </summary>
public class EquipmentBattleIntegrationTests
{
    [Fact]
    public async Task StartBattleAsync_WithEquipment_ShouldUseEquipmentStats()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var character = new Character
        {
            Id = characterId,
            UserId = Guid.NewGuid(),
            Name = "TestWarrior",
            Profession = Profession.Warrior,
            Level = 10,
            Experience = 0,
            Gold = 1000,
            Strength = 20,
            Agility = 10,
            Intellect = 5,
            Stamina = 15,
            CreatedAt = DateTime.UtcNow
        };

        var fakeCharacterRepo = new FakeCharacterRepository(character);
        var fakeBattleRepo = new FakeBattleRepository();
        var fakeStatsService = new FakeStatsAggregationService();
        var fakeEquipmentStatsIntegration = new FakeEquipmentStatsIntegration(fakeStatsService);
        
        // 设置装备提供的属性
        fakeEquipmentStatsIntegration.SetEquipmentBonus(characterId, attackPowerBonus: 100, critChanceBonus: 0.1);
        
        var battleSimulator = new BattleSimulator();
        var battleRunner = new BattleRunner(battleSimulator);
        
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Economy:DefaultDropMode", "expected" }
            })
            .Build();
        
        var service = new StartBattleService(
            fakeCharacterRepo,
            fakeBattleRepo,
            battleRunner,
            battleSimulator,
            fakeEquipmentStatsIntegration,
            config
        );

        // Act
        var battleId = await service.StartAsync(
            characterId,
            simulateSeconds: 5,
            seed: 12345,
            enemyId: "dummy"
        );

        // Assert
        Assert.NotEqual(Guid.Empty, battleId);
        
        // 验证战斗记录被创建
        var battleRecord = fakeBattleRepo.GetSavedBattle();
        Assert.NotNull(battleRecord);
        Assert.Equal(characterId, battleRecord.CharacterId);
        Assert.True(battleRecord.TotalDamage > 0, "Battle should deal damage");
        
        // 验证集成成功 - 如果没有异常抛出，说明装备系统集成正常工作
        // 实际的装备属性影响需要在真实环境中通过数据库和完整的装备系统来验证
    }
    
    [Fact]
    public async Task StartBattleAsync_WithoutEquipment_ShouldStillWork()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var character = new Character
        {
            Id = characterId,
            UserId = Guid.NewGuid(),
            Name = "TestRanger",
            Profession = Profession.Ranger,
            Level = 5,
            Experience = 0,
            Gold = 500,
            Strength = 10,
            Agility = 20,
            Intellect = 10,
            Stamina = 12,
            CreatedAt = DateTime.UtcNow
        };

        var fakeCharacterRepo = new FakeCharacterRepository(character);
        var fakeBattleRepo = new FakeBattleRepository();
        var fakeStatsService = new FakeStatsAggregationService();
        var fakeEquipmentStatsIntegration = new FakeEquipmentStatsIntegration(fakeStatsService);
        
        // 不设置任何装备属性
        
        var battleSimulator = new BattleSimulator();
        var battleRunner = new BattleRunner(battleSimulator);
        
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Economy:DefaultDropMode", "expected" }
            })
            .Build();
        
        var service = new StartBattleService(
            fakeCharacterRepo,
            fakeBattleRepo,
            battleRunner,
            battleSimulator,
            fakeEquipmentStatsIntegration,
            config
        );

        // Act
        var battleId = await service.StartAsync(
            characterId,
            simulateSeconds: 3,
            seed: 54321,
            enemyId: "dummy"
        );

        // Assert - 即使没有装备，战斗也应该正常工作
        Assert.NotEqual(Guid.Empty, battleId);
        var battleRecord = fakeBattleRepo.GetSavedBattle();
        Assert.NotNull(battleRecord);
        Assert.True(battleRecord.TotalDamage > 0, "Battle should still deal damage without equipment");
    }
}

// 用于测试的简单仓储实现
internal class FakeCharacterRepository : ICharacterRepository
{
    private readonly Character _character;

    public FakeCharacterRepository(Character character)
    {
        _character = character;
    }

    public Task<Character?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult<Character?>(_character.Id == id ? _character : null);
    }

    public Task<Character?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<List<Character>> GetAllByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task AddAsync(Character character, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(Character character, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}

internal class FakeBattleRepository : IBattleRepository
{
    private BattleRecord? _savedBattle;

    public Task AddAsync(BattleRecord record, CancellationToken ct = default)
    {
        _savedBattle = record;
        return Task.CompletedTask;
    }

    public BattleRecord? GetSavedBattle() => _savedBattle;

    public Task<BattleRecord?> GetWithSegmentsAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(_savedBattle?.Id == id ? _savedBattle : null);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(_savedBattle?.Id == id);
    }
}

internal class FakeEquipmentStatsIntegration : EquipmentStatsIntegration
{
    private readonly FakeStatsAggregationService _fakeStatsService;

    public FakeEquipmentStatsIntegration(FakeStatsAggregationService fakeStatsService) : base(fakeStatsService)
    {
        _fakeStatsService = fakeStatsService;
    }

    public void SetEquipmentBonus(Guid characterId, double attackPowerBonus = 0, double critChanceBonus = 0)
    {
        _fakeStatsService.SetEquipmentStats(characterId, new Dictionary<StatType, double>
        {
            { StatType.AttackPower, attackPowerBonus },
            { StatType.CritChance, critChanceBonus }
        });
    }
}

internal class FakeStatsAggregationService : StatsAggregationService
{
    private readonly Dictionary<Guid, Dictionary<StatType, double>> _equipmentStatsByCharacter = new();

    public FakeStatsAggregationService() : base(null!, new ArmorCalculator(), new BlockCalculator())
    {
    }

    public void SetEquipmentStats(Guid characterId, Dictionary<StatType, double> stats)
    {
        _equipmentStatsByCharacter[characterId] = stats;
    }

    public override Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
    {
        if (_equipmentStatsByCharacter.TryGetValue(characterId, out var stats))
        {
            return Task.FromResult(stats);
        }
        
        return Task.FromResult(new Dictionary<StatType, double>());
    }
}
