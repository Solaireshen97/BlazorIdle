using BlazorIdle.Server.Api;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Application.Battles.Offline;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// OfflineController API 端点单元测试
/// 测试离线战斗 API 的正确性和集成（简化版，不依赖数据库）
/// </summary>
public class OfflineControllerTests
{
    private readonly Character _testCharacter;
    private readonly OfflineController _controller;
    private readonly InMemoryCharacterRepository _characterRepo;
    private readonly InMemoryActivityPlanRepository _planRepo;

    public OfflineControllerTests()
    {
        // 创建测试角色
        _testCharacter = new Character
        {
            Id = Guid.NewGuid(),
            Name = "TestHero",
            Profession = Profession.Warrior,
            Level = 10,
            Strength = 20,
            Agility = 15,
            Intellect = 10,
            Stamina = 25,
            Gold = 1000,
            Experience = 5000,
            LastSeenAtUtc = DateTime.UtcNow.AddHours(-2) // 2小时前离线
        };

        // 创建简单内存仓储
        _characterRepo = new InMemoryCharacterRepository();
        _characterRepo.AddAsync(_testCharacter).Wait();
        
        _planRepo = new InMemoryActivityPlanRepository();
        var simulator = new BattleSimulator();
        var engine = new OfflineFastForwardEngine(simulator);

        // 创建离线结算服务（不带自动衔接，保持简单）
        var offlineService = new OfflineSettlementService(
            _characterRepo,
            simulator,
            _planRepo,
            engine,
            new FakeDbContext(_characterRepo),
            null  // 不测试自动衔接
        );

        // 创建控制器
        _controller = new OfflineController(offlineService);
    }

    [Fact]
    public async Task CheckOffline_NoOfflineTime_ReturnsNoOfflineResult()
    {
        // Arrange - 角色刚刚在线（稍微延后以确保为正数）
        _testCharacter.LastSeenAtUtc = DateTime.UtcNow.AddSeconds(1); // 稍微延后以确保测试环境中为"未来"

        // Act
        var result = await _controller.CheckOffline(_testCharacter.Id, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var checkResult = Assert.IsType<OfflineCheckResult>(okResult.Value);
        Assert.False(checkResult.HasOfflineTime);
        Assert.True(checkResult.OfflineSeconds <= 1); // 允许小量误差
    }

    [Fact]
    public async Task CheckOffline_NoRunningPlan_ReturnsOfflineTimeOnly()
    {
        // Arrange - 有离线时间但没有运行中的计划
        _testCharacter.LastSeenAtUtc = DateTime.UtcNow.AddHours(-1); // 1小时前离线

        // Act
        var result = await _controller.CheckOffline(_testCharacter.Id, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var checkResult = Assert.IsType<OfflineCheckResult>(okResult.Value);
        Assert.True(checkResult.HasOfflineTime);
        Assert.True(checkResult.OfflineSeconds > 3000); // 至少50分钟
        Assert.False(checkResult.HasRunningPlan);
        Assert.Null(checkResult.Settlement);
    }

    [Fact]
    public async Task CheckOffline_WithRunningPlan_ReturnsSettlement()
    {
        // Arrange - 创建运行中的计划
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = _testCharacter.Id,
            SlotIndex = 0,
            Type = ActivityType.Combat,
            LimitType = LimitType.Duration,
            LimitValue = 3600, // 1小时
            State = ActivityState.Running,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            StartedAt = DateTime.UtcNow.AddHours(-2),
            PayloadJson = JsonSerializer.Serialize(new
            {
                EnemyId = "dummy",
                EnemyCount = 1,
                RespawnDelay = 0.0,
                Seed = (ulong?)null
            }),
            ExecutedSeconds = 1800 // 已执行30分钟
        };
        await _planRepo.AddAsync(plan, CancellationToken.None);

        _testCharacter.LastSeenAtUtc = DateTime.UtcNow.AddHours(-1); // 1小时前离线

        // Act
        var result = await _controller.CheckOffline(_testCharacter.Id, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var checkResult = Assert.IsType<OfflineCheckResult>(okResult.Value);
        Assert.True(checkResult.HasOfflineTime);
        Assert.True(checkResult.HasRunningPlan);
        Assert.NotNull(checkResult.Settlement);
        
        // 验证结算结果
        Assert.True(checkResult.Settlement.SimulatedSeconds > 0);
        Assert.True(checkResult.Settlement.Gold > 0);
        Assert.True(checkResult.Settlement.Exp > 0);
    }

    [Fact]
    public async Task ApplySettlement_ValidRequest_AppliesRewards()
    {
        // Arrange
        var settlement = new OfflineFastForwardResult
        {
            SimulatedSeconds = 1200,
            UpdatedExecutedSeconds = 1200,
            PlanCompleted = false,
            Gold = 500,
            Exp = 1000,
            TotalKills = 10,
            TotalDamage = 5000,
            DropMode = "expected"
        };

        var initialGold = _testCharacter.Gold;
        var initialExp = _testCharacter.Experience;

        var request = new OfflineController.ApplySettlementRequest(
            _testCharacter.Id,
            settlement
        );

        // Act
        var result = await _controller.ApplySettlement(request, CancellationToken.None);

        // Assert
        Assert.IsType<OkResult>(result);

        // 验证收益已发放
        var character = await _characterRepo.GetAsync(_testCharacter.Id);
        Assert.NotNull(character);
        Assert.Equal(initialGold + settlement.Gold, character.Gold);
        Assert.Equal(initialExp + settlement.Exp, character.Experience);
    }

    [Fact]
    public async Task CheckOffline_CharacterNotFound_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _controller.CheckOffline(nonExistentId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task ApplySettlement_CharacterNotFound_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var settlement = new OfflineFastForwardResult
        {
            SimulatedSeconds = 1200,
            UpdatedExecutedSeconds = 1200,
            PlanCompleted = false,
            Gold = 500,
            Exp = 1000,
            TotalKills = 10,
            TotalDamage = 5000,
            DropMode = "expected"
        };

        var request = new OfflineController.ApplySettlementRequest(
            nonExistentId,
            settlement
        );

        // Act
        var result = await _controller.ApplySettlement(request, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // 简单内存仓储实现（用于测试）
    private class InMemoryCharacterRepository : ICharacterRepository
    {
        private readonly Dictionary<Guid, Character> _characters = new();

        public Task<Character?> GetAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_characters.TryGetValue(id, out var character) ? character : null);

        public Task AddAsync(Character character, CancellationToken ct = default)
        {
            _characters[character.Id] = character;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Character character, CancellationToken ct = default)
            => Task.CompletedTask; // 已经在内存中更新

        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_characters.Remove(id));
    }

    private class InMemoryActivityPlanRepository : IActivityPlanRepository
    {
        private readonly List<ActivityPlan> _plans = new();

        public Task<ActivityPlan?> GetAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_plans.FirstOrDefault(p => p.Id == id));

        public Task<List<ActivityPlan>> GetByCharacterAsync(Guid characterId, CancellationToken ct = default)
            => Task.FromResult(_plans.Where(p => p.CharacterId == characterId).ToList());

        public Task<List<ActivityPlan>> GetByCharacterAndSlotAsync(Guid characterId, int slotIndex, CancellationToken ct = default)
            => Task.FromResult(_plans.Where(p => p.CharacterId == characterId && p.SlotIndex == slotIndex).ToList());

        public Task AddAsync(ActivityPlan plan, CancellationToken ct = default)
        {
            _plans.Add(plan);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ActivityPlan plan, CancellationToken ct = default)
            => Task.CompletedTask; // 已经在内存中更新

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            _plans.RemoveAll(p => p.Id == id);
            return Task.CompletedTask;
        }

        public Task<ActivityPlan?> GetRunningPlanAsync(Guid characterId, CancellationToken ct = default)
            => Task.FromResult(_plans.FirstOrDefault(p => p.CharacterId == characterId && p.State == ActivityState.Running));

        public Task<ActivityPlan?> GetNextPendingPlanAsync(Guid characterId, CancellationToken ct = default)
            => Task.FromResult(_plans
                .Where(p => p.CharacterId == characterId && p.State == ActivityState.Pending)
                .OrderBy(p => p.SlotIndex)
                .ThenBy(p => p.CreatedAt)
                .FirstOrDefault());

        public Task<List<ActivityPlan>> GetAllRunningPlansAsync(CancellationToken ct = default)
            => Task.FromResult(_plans.Where(p => p.State == ActivityState.Running).ToList());
    }

    // Fake DbContext for SaveChangesAsync
    private class FakeDbContext : BlazorIdle.Server.Infrastructure.Persistence.GameDbContext
    {
        private readonly InMemoryCharacterRepository _charRepo;

        public FakeDbContext(InMemoryCharacterRepository charRepo) 
            : base(new Microsoft.EntityFrameworkCore.DbContextOptions<BlazorIdle.Server.Infrastructure.Persistence.GameDbContext>())
        {
            _charRepo = charRepo;
        }

        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
            => Task.FromResult(0); // 不需要实际保存
    }
}
