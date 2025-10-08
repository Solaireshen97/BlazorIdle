using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Application.Activities;

/// <summary>
/// 活动计划服务：管理活动计划的创建、启动、停止和状态更新
/// </summary>
public class ActivityPlanService
{
    private readonly IActivityPlanRepository _plans;
    private readonly ICharacterRepository _characters;
    private readonly StepBattleCoordinator _coordinator;

    public ActivityPlanService(
        IActivityPlanRepository plans,
        ICharacterRepository characters,
        StepBattleCoordinator coordinator)
    {
        _plans = plans;
        _characters = characters;
        _coordinator = coordinator;
    }

    /// <summary>
    /// 创建活动计划
    /// </summary>
    public async Task<ActivityPlan> CreatePlanAsync(
        Guid characterId,
        int slotIndex,
        ActivityType type,
        LimitType limitType,
        double? limitValue,
        string payloadJson,
        CancellationToken ct = default)
    {
        // 验证角色是否存在
        var character = await _characters.GetAsync(characterId, ct);
        if (character is null)
            throw new InvalidOperationException("Character not found");

        // 验证槽位索引
        if (slotIndex < 0 || slotIndex >= 5)
            throw new ArgumentException("SlotIndex must be between 0 and 4", nameof(slotIndex));

        // 创建计划
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            SlotIndex = slotIndex,
            Type = type,
            LimitType = limitType,
            LimitValue = limitValue,
            State = ActivityState.Pending,
            CreatedAt = DateTime.UtcNow,
            PayloadJson = payloadJson,
            ExecutedSeconds = 0
        };

        await _plans.AddAsync(plan, ct);

        // 自动启动：如果当前角色没有正在运行的任务，自动启动这个任务
        var runningPlan = await _plans.GetRunningPlanAsync(characterId, ct);
        if (runningPlan is null)
        {
            try
            {
                await StartPlanAsync(plan.Id, ct);
            }
            catch (Exception)
            {
                // 如果自动启动失败，保持计划为Pending状态，不影响计划创建
                // 记录日志或忽略错误
            }
        }

        return plan;
    }

    /// <summary>
    /// 恢复暂停的活动计划
    /// </summary>
    public async Task<Guid> ResumePlanAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _plans.GetAsync(planId, ct);
        if (plan is null)
            throw new InvalidOperationException("Plan not found");

        if (plan.State != ActivityState.Running)
            throw new InvalidOperationException($"Cannot resume plan in state {plan.State}");

        // 如果计划已经有战斗ID（已在运行），直接返回
        if (plan.BattleId.HasValue)
        {
            return plan.BattleId.Value;
        }

        // 检查是否有其他正在运行的战斗（不包括暂停的计划）
        var runningPlan = await _plans.GetRunningPlanAsync(plan.CharacterId, ct);
        if (runningPlan is not null && runningPlan.BattleId.HasValue && runningPlan.Id != plan.Id)
            throw new InvalidOperationException("Another plan is already running");

        // 获取角色数据
        var character = await _characters.GetAsync(plan.CharacterId, ct);
        if (character is null)
            throw new InvalidOperationException("Character not found");

        var profession = character.Profession;
        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        var attrs = new PrimaryAttributes(character.Strength, character.Agility, character.Intellect, character.Stamina);
        var derived = StatsBuilder.BuildDerived(profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        // 加载战斗状态快照
        Battles.Offline.BattleState? battleState = null;
        if (!string.IsNullOrWhiteSpace(plan.BattleStateJson))
        {
            try
            {
                battleState = JsonSerializer.Deserialize<Battles.Offline.BattleState>(plan.BattleStateJson);
            }
            catch
            {
                // 如果反序列化失败，忽略快照，从头开始
                battleState = null;
            }
        }

        // 根据活动类型启动战斗
        Guid battleId;
        if (plan.Type == ActivityType.Combat)
        {
            var payload = JsonSerializer.Deserialize<CombatActivityPayload>(plan.PayloadJson);
            if (payload is null)
                throw new InvalidOperationException("Invalid combat payload");

            var seed = payload.Seed ?? DeriveSeed(character.Id);
            var duration = plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue
                ? plan.LimitValue.Value
                : 86400.0; // 24小时作为无限模式的默认值

            battleId = _coordinator.Start(
                character.Id,
                profession,
                stats,
                duration,
                seed,
                payload.EnemyId,
                payload.EnemyCount,
                StepBattleMode.Continuous,
                dungeonId: null,
                continuousRespawnDelaySeconds: payload.RespawnDelay,
                dungeonWaveDelaySeconds: null,
                dungeonRunDelaySeconds: null,
                battleState: battleState
            );
        }
        else if (plan.Type == ActivityType.Dungeon)
        {
            var payload = JsonSerializer.Deserialize<DungeonActivityPayload>(plan.PayloadJson);
            if (payload is null)
                throw new InvalidOperationException("Invalid dungeon payload");

            var seed = payload.Seed ?? DeriveSeed(character.Id);
            var duration = plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue
                ? plan.LimitValue.Value
                : 86400.0;

            var mode = payload.Loop ? StepBattleMode.DungeonLoop : StepBattleMode.DungeonSingle;

            battleId = _coordinator.Start(
                character.Id,
                profession,
                stats,
                duration,
                seed,
                enemyId: null,
                enemyCount: 1,
                mode,
                dungeonId: payload.DungeonId,
                continuousRespawnDelaySeconds: null,
                dungeonWaveDelaySeconds: payload.WaveDelay,
                dungeonRunDelaySeconds: payload.RunDelay,
                battleState: battleState
            );
        }
        else
        {
            throw new NotImplementedException($"Activity type {plan.Type} is not implemented");
        }

        // 更新计划状态
        plan.StartedAt = DateTime.UtcNow;
        plan.BattleId = battleId;
        await _plans.UpdateAsync(plan, ct);

        return battleId;
    }

    /// <summary>
    /// 启动活动计划
    /// </summary>
    public async Task<Guid> StartPlanAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _plans.GetAsync(planId, ct);
        if (plan is null)
            throw new InvalidOperationException("Plan not found");

        if (plan.State != ActivityState.Pending)
            throw new InvalidOperationException($"Cannot start plan in state {plan.State}");

        // 检查是否有其他正在运行的计划（不包括暂停的计划）
        var runningPlan = await _plans.GetRunningPlanAsync(plan.CharacterId, ct);
        if (runningPlan is not null && runningPlan.BattleId.HasValue)
            throw new InvalidOperationException("Another plan is already running");

        // 获取角色数据
        var character = await _characters.GetAsync(plan.CharacterId, ct);
        if (character is null)
            throw new InvalidOperationException("Character not found");

        var profession = character.Profession;
        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        var attrs = new PrimaryAttributes(character.Strength, character.Agility, character.Intellect, character.Stamina);
        var derived = StatsBuilder.BuildDerived(profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        // 加载战斗状态快照（如果有）
        Battles.Offline.BattleState? battleState = null;
        if (!string.IsNullOrWhiteSpace(plan.BattleStateJson))
        {
            try
            {
                battleState = JsonSerializer.Deserialize<Battles.Offline.BattleState>(plan.BattleStateJson);
            }
            catch
            {
                // 如果反序列化失败，忽略快照，从头开始
                battleState = null;
            }
        }

        // 根据活动类型启动战斗
        Guid battleId;
        if (plan.Type == ActivityType.Combat)
        {
            var payload = JsonSerializer.Deserialize<CombatActivityPayload>(plan.PayloadJson);
            if (payload is null)
                throw new InvalidOperationException("Invalid combat payload");

            var seed = payload.Seed ?? DeriveSeed(character.Id);
            var duration = plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue
                ? plan.LimitValue.Value
                : 86400.0; // 24小时作为无限模式的默认值

            battleId = _coordinator.Start(
                character.Id,
                profession,
                stats,
                duration,
                seed,
                payload.EnemyId,
                payload.EnemyCount,
                StepBattleMode.Continuous,
                dungeonId: null,
                continuousRespawnDelaySeconds: payload.RespawnDelay,
                dungeonWaveDelaySeconds: null,
                dungeonRunDelaySeconds: null,
                battleState: battleState
            );
        }
        else if (plan.Type == ActivityType.Dungeon)
        {
            var payload = JsonSerializer.Deserialize<DungeonActivityPayload>(plan.PayloadJson);
            if (payload is null)
                throw new InvalidOperationException("Invalid dungeon payload");

            var seed = payload.Seed ?? DeriveSeed(character.Id);
            var duration = plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue
                ? plan.LimitValue.Value
                : 86400.0;

            var mode = payload.Loop ? StepBattleMode.DungeonLoop : StepBattleMode.DungeonSingle;

            battleId = _coordinator.Start(
                character.Id,
                profession,
                stats,
                duration,
                seed,
                enemyId: null,
                enemyCount: 1,
                mode,
                dungeonId: payload.DungeonId,
                continuousRespawnDelaySeconds: null,
                dungeonWaveDelaySeconds: payload.WaveDelay,
                dungeonRunDelaySeconds: payload.RunDelay,
                battleState: battleState
            );
        }
        else
        {
            throw new NotImplementedException($"Activity type {plan.Type} is not implemented");
        }

        // 更新计划状态
        plan.State = ActivityState.Running;
        plan.StartedAt = DateTime.UtcNow;
        plan.BattleId = battleId;
        await _plans.UpdateAsync(plan, ct);

        return battleId;
    }

    /// <summary>
    /// 暂停活动计划（用于离线检测，保留状态以便恢复）
    /// </summary>
    public async Task<bool> PausePlanAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _plans.GetAsync(planId, ct);
        if (plan is null)
            return false;

        if (plan.State != ActivityState.Running)
            return false;

        // 保存战斗状态（在停止前）
        if (plan.BattleId.HasValue)
        {
            if (_coordinator.TryGet(plan.BattleId.Value, out var rb) && rb != null)
            {
                var battleState = rb.Engine.CaptureBattleState();
                plan.BattleStateJson = JsonSerializer.Serialize(battleState);
            }
        }

        // 更新已执行时长
        if (plan.StartedAt.HasValue)
        {
            var elapsed = (DateTime.UtcNow - plan.StartedAt.Value).TotalSeconds;
            plan.ExecutedSeconds = elapsed;
        }

        // 停止战斗并清空内存
        if (plan.BattleId.HasValue)
        {
            await _coordinator.StopAndFinalizeAsync(plan.BattleId.Value, ct);
            plan.BattleId = null; // 清空战斗ID，但保留状态
        }

        // 保持计划在 Running 状态，但清空 StartedAt（表示暂停）
        // plan.State 保持为 Running
        plan.StartedAt = null;

        await _plans.UpdateAsync(plan, ct);

        return true;
    }

    /// <summary>
    /// 停止活动计划
    /// </summary>
    public async Task<bool> StopPlanAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _plans.GetAsync(planId, ct);
        if (plan is null)
            return false;

        if (plan.State != ActivityState.Running)
            return false;

        // 保存战斗状态（在停止前）
        if (plan.BattleId.HasValue)
        {
            if (_coordinator.TryGet(plan.BattleId.Value, out var rb) && rb != null)
            {
                var battleState = rb.Engine.CaptureBattleState();
                plan.BattleStateJson = JsonSerializer.Serialize(battleState);
            }
        }

        // 停止战斗
        if (plan.BattleId.HasValue)
        {
            await _coordinator.StopAndFinalizeAsync(plan.BattleId.Value, ct);
        }

        // 更新计划状态
        plan.State = ActivityState.Completed;
        plan.CompletedAt = DateTime.UtcNow;
        
        // 更新已执行时长
        if (plan.StartedAt.HasValue)
        {
            var elapsed = (DateTime.UtcNow - plan.StartedAt.Value).TotalSeconds;
            plan.ExecutedSeconds = elapsed;
        }
        
        // 清空战斗状态（计划已完成）
        plan.BattleStateJson = null;

        await _plans.UpdateAsync(plan, ct);

        // 自动启动下一个待执行的任务
        await TryStartNextPendingPlanAsync(plan.CharacterId, ct);

        return true;
    }

    /// <summary>
    /// 更新运行中计划的执行进度
    /// </summary>
    public async Task UpdatePlanProgressAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _plans.GetAsync(planId, ct);
        if (plan is null || plan.State != ActivityState.Running)
            return;

        // 获取战斗状态
        if (plan.BattleId.HasValue && _coordinator.TryGet(plan.BattleId.Value, out var rb) && rb is not null)
        {
            plan.ExecutedSeconds = rb.Clock.CurrentTime;
            
            // 定期保存战斗状态快照（用于离线/在线无缝切换）
            var battleState = rb.Engine.CaptureBattleState();
            plan.BattleStateJson = JsonSerializer.Serialize(battleState);

            // 检查是否达到限制条件
            if (plan.IsLimitReached())
            {
                await StopPlanAsync(planId, ct);
            }
            else
            {
                await _plans.UpdateAsync(plan, ct);
            }
        }
    }

    /// <summary>
    /// 取消活动计划
    /// </summary>
    public async Task<bool> CancelPlanAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _plans.GetAsync(planId, ct);
        if (plan is null)
            return false;

        // 如果正在运行，先停止
        if (plan.State == ActivityState.Running)
        {
            if (plan.BattleId.HasValue)
            {
                await _coordinator.StopAndFinalizeAsync(plan.BattleId.Value, ct);
            }

            if (plan.StartedAt.HasValue)
            {
                var elapsed = (DateTime.UtcNow - plan.StartedAt.Value).TotalSeconds;
                plan.ExecutedSeconds = elapsed;
            }
        }

        plan.State = ActivityState.Cancelled;
        plan.CompletedAt = DateTime.UtcNow;
        await _plans.UpdateAsync(plan, ct);
        return true;
    }

    /// <summary>
    /// 尝试启动下一个待执行的任务（公开方法，供离线结算等场景调用）
    /// </summary>
    public async Task<ActivityPlan?> TryStartNextPendingPlanAsync(Guid characterId, CancellationToken ct = default)
    {
        // 获取下一个待执行的任务（按槽位和创建时间排序）
        var nextPlan = await _plans.GetNextPendingPlanAsync(characterId, ct);
        if (nextPlan is not null)
        {
            try
            {
                await StartPlanAsync(nextPlan.Id, ct);
                return nextPlan;
            }
            catch (Exception)
            {
                // 如果启动失败，保持计划为Pending状态
                // 记录日志或忽略错误
                return null;
            }
        }
        return null;
    }

    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}
