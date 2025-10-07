using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Activity;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BlazorIdle.Server.Application.Activities;

/// <summary>
/// 战斗活动执行器：封装现有的 StepBattleCoordinator
/// </summary>
public sealed class CombatActivityExecutor : IActivityExecutor
{
    private readonly StepBattleCoordinator _battleCoordinator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CombatActivityExecutor> _logger;
    
    public ActivityType SupportedType => ActivityType.Combat;
    
    public CombatActivityExecutor(StepBattleCoordinator battleCoordinator, IServiceScopeFactory scopeFactory, ILogger<CombatActivityExecutor> logger)
    {
        _battleCoordinator = battleCoordinator;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }
    
    public async Task<ActivityExecutionContext> StartAsync(ActivityPlan plan, CancellationToken ct = default)
    {
        if (plan.Type != ActivityType.Combat)
            throw new InvalidOperationException($"CombatActivityExecutor only supports Combat activities, got {plan.Type}");
        
        // 解析战斗载荷
        var payload = JsonSerializer.Deserialize<CombatPayload>(plan.PayloadJson) 
            ?? throw new InvalidOperationException("Invalid combat payload");
        
        // 创建临时 Scope 并获取 Repository
        using var scope = _scopeFactory.CreateScope();
        var characters = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
        
        // 获取角色信息
        var character = await characters.GetAsync(plan.CharacterId, ct) 
            ?? throw new InvalidOperationException($"Character {plan.CharacterId} not found");
        
        // 构建角色属性
        var baseStats = ProfessionBaseStatsRegistry.Resolve(character.Profession);
        var attrs = new PrimaryAttributes(character.Strength, character.Agility, character.Intellect, character.Stamina);
        var derived = StatsBuilder.BuildDerived(character.Profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);
        
        // 生成种子
        ulong seed = payload.Seed ?? DeriveSeed(plan.CharacterId);
        
        // 解析战斗模式
        var mode = ParseBattleMode(payload.Mode);
        
        // 计算目标时长
        // 对于Duration模式：使用活动计划的时长限制
        // 对于Continuous模式：使用一个很大的值，让活动计划的限制来控制完成
        double targetSeconds;
        if (mode == StepBattleMode.Duration)
        {
            // Duration模式：战斗自己会在达到目标时间时完成
            targetSeconds = plan.Limit is DurationLimit dl ? dl.DurationSeconds : 3600.0;
        }
        else
        {
            // Continuous/Dungeon模式：战斗不会自动完成，由活动计划的限制控制
            // 使用一个很大的值以确保战斗不会因为时间限制而停止
            targetSeconds = 86400.0; // 24小时，足够长
        }
        
        // 启动战斗
        var battleId = _battleCoordinator.Start(
            characterId: plan.CharacterId,
            profession: character.Profession,
            stats: stats,
            seconds: targetSeconds,
            seed: seed,
            enemyId: payload.EnemyId,
            enemyCount: payload.EnemyCount ?? 1,
            mode: mode,
            dungeonId: payload.DungeonId,
            continuousRespawnDelaySeconds: payload.RespawnDelay,
            dungeonWaveDelaySeconds: payload.WaveDelay,
            dungeonRunDelaySeconds: payload.RunDelay
        );
        
        // 构建执行上下文
        var context = new ActivityExecutionContext
        {
            PlanId = plan.Id,
            UnderlyingExecutionId = battleId
        };
        
        context.Data["seed"] = seed;
        context.Data["mode"] = mode.ToString();
        context.Data["targetSeconds"] = targetSeconds;
        
        _logger.LogInformation(
            "Started combat activity plan {PlanId} for character {CharacterId}: mode={Mode}, targetSeconds={TargetSeconds}, limit={LimitType}",
            plan.Id, plan.CharacterId, mode, targetSeconds, plan.Limit.GetLimitType());
        
        return context;
    }
    
    public async Task AdvanceAsync(ActivityPlan plan, ActivityExecutionContext context, CancellationToken ct = default)
    {
        // StepBattleCoordinator 由 HostedService 统一推进，这里只需要更新进度
        if (context.UnderlyingExecutionId is null)
            return;
        
        var battleId = context.UnderlyingExecutionId.Value;
        
        // 获取战斗状态
        if (!_battleCoordinator.TryGet(battleId, out var battle) || battle is null)
        {
            _logger.LogWarning("Battle {BattleId} not found for activity plan {PlanId}", battleId, plan.Id);
            return;
        }
        
        // 更新活动进度
        var simulatedSeconds = battle.Clock.CurrentTime;
        var completedCount = CalculateCompletedCount(battle, plan.Limit);
        
        plan.UpdateProgress(simulatedSeconds, completedCount);
        context.LastUpdatedAtUtc = DateTime.UtcNow;
        
        await Task.CompletedTask;
    }
    
    public Task StopAsync(ActivityPlan plan, ActivityExecutionContext context, CancellationToken ct = default)
    {
        if (context.UnderlyingExecutionId is null)
            return Task.CompletedTask;
        
        var battleId = context.UnderlyingExecutionId.Value;
        
        _logger.LogInformation(
            "Stopping combat activity plan {PlanId} for character {CharacterId}: battleId={BattleId}",
            plan.Id, plan.CharacterId, battleId);
        
        // 停止并持久化战斗
        return _battleCoordinator.StopAndFinalizeAsync(battleId, ct).ContinueWith(_ => { }, ct);
    }
    
    public async Task<bool> CheckCompletionAsync(ActivityPlan plan, ActivityExecutionContext context, CancellationToken ct = default)
    {
        if (context.UnderlyingExecutionId is null)
            return true; // 如果没有底层执行，认为已完成
        
        var battleId = context.UnderlyingExecutionId.Value;
        
        // 检查战斗是否完成
        if (!_battleCoordinator.TryGet(battleId, out var battle) || battle is null)
        {
            _logger.LogWarning("Battle {BattleId} not found for activity plan {PlanId}, marking as completed", battleId, plan.Id);
            return true; // 战斗已不在内存，认为已完成
        }
        
        // 如果战斗已完成，返回完成
        if (battle.Completed)
        {
            _logger.LogInformation("Battle {BattleId} completed for activity plan {PlanId}", battleId, plan.Id);
            return true;
        }
        
        // 检查活动计划的限制是否达到
        // 注意：对于非Duration模式的战斗，需要依靠活动计划的限制来决定何时完成
        var limitReached = plan.IsLimitReached();
        if (limitReached)
        {
            _logger.LogInformation(
                "Activity plan {PlanId} limit reached: simulated={SimulatedSeconds}s, count={CompletedCount}, limit={LimitType}",
                plan.Id, plan.Progress.SimulatedSeconds, plan.Progress.CompletedCount, plan.Limit.GetLimitType());
        }
        
        return limitReached;
    }
    
    private static StepBattleMode ParseBattleMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return StepBattleMode.Duration;
        
        return mode.Trim().ToLowerInvariant() switch
        {
            "continuous" => StepBattleMode.Continuous,
            "dungeon" => StepBattleMode.DungeonSingle,
            "dungeonloop" => StepBattleMode.DungeonLoop,
            _ => StepBattleMode.Duration
        };
    }
    
    private static int CalculateCompletedCount(RunningBattle battle, LimitSpec limit)
    {
        // 如果是计数限制，从 segments 中统计击杀数
        if (limit is CountLimit)
        {
            int totalKills = 0;
            foreach (var seg in battle.Segments)
            {
                foreach (var (tag, count) in seg.TagCounters)
                {
                    if (tag.StartsWith("kill.", StringComparison.Ordinal))
                        totalKills += count;
                }
            }
            return totalKills;
        }
        
        // 其他限制类型返回0
        return 0;
    }
    
    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}

/// <summary>
/// 战斗活动载荷
/// </summary>
public sealed class CombatPayload
{
    public string? EnemyId { get; set; }
    public int? EnemyCount { get; set; }
    public string? Mode { get; set; }
    public string? DungeonId { get; set; }
    public double? RespawnDelay { get; set; }
    public double? WaveDelay { get; set; }
    public double? RunDelay { get; set; }
    public ulong? Seed { get; set; }
}
