using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Activities;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Records;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试多次服务器重启场景，验证快照不会累积
/// 这是修复"第二次重启失败"问题的关键测试
/// </summary>
public class MultiRestartRecoveryTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly GameDbContext _dbContext;
    private readonly ITestOutputHelper _output;

    public MultiRestartRecoveryTests(ITestOutputHelper output)
    {
        _output = output;
        var services = new ServiceCollection();
        
        // 使用唯一的数据库名称，但在所有作用域中共享
        var dbName = $"TestDb_{Guid.NewGuid()}";
        services.AddDbContext<GameDbContext>(options =>
            options.UseInMemoryDatabase(dbName), ServiceLifetime.Scoped);
        
        // 添加配置
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Combat:RewardFlushIntervalSeconds", "10.0"},
                {"Combat:EnablePeriodicRewards", "true"}
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        
        services.AddScoped<IActivityPlanRepository, BlazorIdle.Server.Infrastructure.Persistence.Repositories.ActivityPlanRepository>();
        services.AddScoped<ICharacterRepository, BlazorIdle.Server.Infrastructure.Persistence.Repositories.CharacterRepository>();
        services.AddSingleton<StepBattleCoordinator>();
        services.AddSingleton<StepBattleSnapshotService>();
        services.AddScoped<EquipmentStatsIntegration>();
        services.AddScoped<StatsAggregationService>();
        services.AddScoped<EquipmentService>();
        services.AddSingleton<ArmorCalculator>();
        services.AddSingleton<BlockCalculator>();
        services.AddSingleton<AttackSpeedCalculator>();
        services.AddSingleton<WeaponDamageCalculator>();
        
        // 添加日志
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<GameDbContext>();
    }

    [Fact]
    public async Task SecondRestart_ShouldNotAccumulateSnapshots()
    {
        // 这个测试模拟了问题描述的场景：
        // 1. 创建战斗快照
        // 2. 模拟第一次恢复（应该删除快照）
        // 3. 创建新快照
        // 4. 模拟第二次恢复（应该也能删除快照，不累积）
        
        // Arrange: 创建测试角色和快照
        var characterId = Guid.NewGuid();
        var character = new Character
        {
            Id = characterId,
            UserId = Guid.NewGuid(),
            Name = "MultiRestartTestCharacter",
            Profession = Profession.Warrior,
            Level = 10,
            Experience = 0,
            Gold = 100,
            Strength = 20,
            Agility = 15,
            Intellect = 10,
            Stamina = 18,
            LastSeenAtUtc = DateTime.UtcNow
        };
        _dbContext.Characters.Add(character);
        
        // 创建第一个快照（模拟服务器关闭前保存的快照）
        var snapshot1 = CreateTestSnapshot(characterId, Profession.Warrior, 10.0);
        _dbContext.Set<RunningBattleSnapshotRecord>().Add(snapshot1);
        await _dbContext.SaveChangesAsync();
        
        _output.WriteLine($"第一次运行: 创建快照 {snapshot1.Id}");
        
        // 验证快照已保存
        var snapshotCount1 = await _dbContext.Set<RunningBattleSnapshotRecord>().CountAsync();
        Assert.Equal(1, snapshotCount1);
        _output.WriteLine($"第一次运行: 快照数量 = {snapshotCount1}");

        // Act & Assert: 模拟第一次重启和恢复
        var snapshotService = _serviceProvider.GetRequiredService<StepBattleSnapshotService>();
        var coordinator = _serviceProvider.GetRequiredService<StepBattleCoordinator>();
        
        await snapshotService.RecoverAllAsync(coordinator, CancellationToken.None);
        
        // 刷新 DbContext 以看到其他scope的更改
        _dbContext.ChangeTracker.Clear();
        
        // 关键验证：快照应该被删除（修复的关键点）
        var snapshotCount2 = await _dbContext.Set<RunningBattleSnapshotRecord>().CountAsync();
        _output.WriteLine($"第一次重启后: 快照数量 = {snapshotCount2}");
        Assert.Equal(0, snapshotCount2); // 旧快照应该被删除

        // 创建第二个快照（模拟继续运行后再次关闭）
        var snapshot2 = CreateTestSnapshot(characterId, Profession.Warrior, 20.0);
        _dbContext.Set<RunningBattleSnapshotRecord>().Add(snapshot2);
        await _dbContext.SaveChangesAsync();
        
        var snapshotCount3 = await _dbContext.Set<RunningBattleSnapshotRecord>().CountAsync();
        _output.WriteLine($"保存新快照后: 快照数量 = {snapshotCount3}");
        Assert.Equal(1, snapshotCount3); // 只有新快照

        // 模拟第二次重启：这是之前失败的关键场景
        await snapshotService.RecoverAllAsync(coordinator, CancellationToken.None);
        
        // 刷新 DbContext 以看到其他scope的更改
        _dbContext.ChangeTracker.Clear();
        
        // 验证没有快照累积
        var snapshotCount4 = await _dbContext.Set<RunningBattleSnapshotRecord>().CountAsync();
        _output.WriteLine($"第二次重启后: 快照数量 = {snapshotCount4}");
        Assert.Equal(0, snapshotCount4); // 所有旧快照都应该被清理

        _output.WriteLine("测试通过：第二次重启成功，没有快照累积");
    }
    
    private RunningBattleSnapshotRecord CreateTestSnapshot(Guid characterId, Profession profession, double simulatedSeconds)
    {
        var battleId = Guid.NewGuid();
        var dto = new
        {
            StepBattleId = battleId,
            CharacterId = characterId,
            Profession = (int)profession,
            EnemyId = "goblin",
            EnemyCount = 1,
            Seed = (ulong)12345,
            TargetSeconds = 300.0,
            SimulatedSeconds = simulatedSeconds,
            Segments = new List<CombatSegment>()
        };
        
        return new RunningBattleSnapshotRecord
        {
            Id = Guid.NewGuid(),
            StepBattleId = battleId,
            CharacterId = characterId,
            Profession = (int)profession,
            EnemyId = "goblin",
            EnemyCount = 1,
            Seed = "12345",
            TargetSeconds = 300,
            SimulatedSeconds = simulatedSeconds,
            UpdatedAtUtc = DateTime.UtcNow,
            SnapshotJson = JsonSerializer.Serialize(dto)
        };
    }

    [Fact]
    public async Task MultipleRestarts_WithMultipleBattles_ShouldCleanupProperly()
    {
        // 测试多个战斗、多次重启的场景
        
        // 创建3个测试角色
        var characterIds = new List<Guid>();
        for (int i = 0; i < 3; i++)
        {
            var charId = Guid.NewGuid();
            characterIds.Add(charId);
            var character = new Character
            {
                Id = charId,
                UserId = Guid.NewGuid(),
                Name = $"TestCharacter{i}",
                Profession = Profession.Warrior,
                Level = 5 + i,
                Experience = 0,
                Gold = 50,
                Strength = 15 + i,
                Agility = 12 + i,
                Intellect = 10,
                Stamina = 14 + i,
                LastSeenAtUtc = DateTime.UtcNow
            };
            _dbContext.Characters.Add(character);
            
            // 为每个角色创建快照
            var snapshot = CreateTestSnapshot(charId, Profession.Warrior, 10.0 + i);
            _dbContext.Set<RunningBattleSnapshotRecord>().Add(snapshot);
        }
        await _dbContext.SaveChangesAsync();

        var snapshotService = _serviceProvider.GetRequiredService<StepBattleSnapshotService>();
        var coordinator = _serviceProvider.GetRequiredService<StepBattleCoordinator>();

        var initialSnapshotCount = await _dbContext.Set<RunningBattleSnapshotRecord>().CountAsync();
        _output.WriteLine($"初始快照数量: {initialSnapshotCount}");
        Assert.Equal(3, initialSnapshotCount);

        // 执行多次重启周期
        for (int restart = 1; restart <= 3; restart++)
        {
            _output.WriteLine($"\n=== 第 {restart} 次重启 ===");
            
            // 模拟恢复
            await snapshotService.RecoverAllAsync(coordinator, CancellationToken.None);
            
            // 刷新 DbContext 以看到其他scope的更改
            _dbContext.ChangeTracker.Clear();
            
            // 验证旧快照已清理
            var snapshotsAfterRecovery = await _dbContext.Set<RunningBattleSnapshotRecord>().CountAsync();
            _output.WriteLine($"恢复后快照数量: {snapshotsAfterRecovery}");
            Assert.Equal(0, snapshotsAfterRecovery);
            
            // 创建新快照（模拟继续运行）
            if (restart < 3) // 最后一次不需要再创建
            {
                foreach (var charId in characterIds)
                {
                    var snapshot = CreateTestSnapshot(charId, Profession.Warrior, 10.0 * (restart + 1));
                    _dbContext.Set<RunningBattleSnapshotRecord>().Add(snapshot);
                }
                await _dbContext.SaveChangesAsync();
                
                var snapshotsAfterSave = await _dbContext.Set<RunningBattleSnapshotRecord>().CountAsync();
                _output.WriteLine($"保存新快照后数量: {snapshotsAfterSave}");
                Assert.Equal(3, snapshotsAfterSave);
            }
        }

        _output.WriteLine("\n测试通过：多次重启没有快照累积问题");
    }

    [Fact]
    public async Task InvalidSnapshot_ShouldBeDeleted_NotCauseFailure()
    {
        // 测试损坏的快照不会导致启动失败
        
        var coordinator = _serviceProvider.GetRequiredService<StepBattleCoordinator>();
        var snapshotService = _serviceProvider.GetRequiredService<StepBattleSnapshotService>();

        // 创建一个损坏的快照（无效的JSON）
        var invalidSnapshot = new RunningBattleSnapshotRecord
        {
            Id = Guid.NewGuid(),
            StepBattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(), // 不存在的角色
            Profession = (int)Profession.Warrior,
            EnemyId = "goblin",
            EnemyCount = 1,
            Seed = "12345",
            TargetSeconds = 300,
            SimulatedSeconds = 10,
            UpdatedAtUtc = DateTime.UtcNow,
            SnapshotJson = "{invalid json}" // 损坏的JSON
        };
        _dbContext.Set<RunningBattleSnapshotRecord>().Add(invalidSnapshot);

        // 创建一个角色不存在的快照
        var orphanedSnapshot = new RunningBattleSnapshotRecord
        {
            Id = Guid.NewGuid(),
            StepBattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(), // 不存在的角色
            Profession = (int)Profession.Ranger,
            EnemyId = "wolf",
            EnemyCount = 2,
            Seed = "54321",
            TargetSeconds = 300,
            SimulatedSeconds = 20,
            UpdatedAtUtc = DateTime.UtcNow,
            SnapshotJson = @"{""StepBattleId"":""00000000-0000-0000-0000-000000000000"",""CharacterId"":""00000000-0000-0000-0000-000000000000"",""Profession"":1,""EnemyId"":""wolf"",""EnemyCount"":2,""Seed"":54321,""TargetSeconds"":300,""SimulatedSeconds"":20,""Segments"":[]}"
        };
        _dbContext.Set<RunningBattleSnapshotRecord>().Add(orphanedSnapshot);

        await _dbContext.SaveChangesAsync();

        var initialCount = await _dbContext.Set<RunningBattleSnapshotRecord>().CountAsync();
        _output.WriteLine($"初始快照数量（包含损坏的）: {initialCount}");
        Assert.Equal(2, initialCount);

        // 尝试恢复 - 应该不会崩溃
        await snapshotService.RecoverAllAsync(coordinator, CancellationToken.None);

        // 刷新 DbContext 以看到其他scope的更改
        _dbContext.ChangeTracker.Clear();

        // 验证损坏的快照已被删除
        var finalCount = await _dbContext.Set<RunningBattleSnapshotRecord>().CountAsync();
        _output.WriteLine($"清理后快照数量: {finalCount}");
        Assert.Equal(0, finalCount);

        _output.WriteLine("测试通过：损坏的快照被正确清理，没有导致启动失败");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}
