using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Activities;
using BlazorIdle.Server.Domain.Activities;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/activity-plans")]
public class ActivityPlansController : ControllerBase
{
    private readonly ActivityPlanService _service;
    private readonly IActivityPlanRepository _repository;

    public ActivityPlansController(ActivityPlanService service, IActivityPlanRepository repository)
    {
        _service = service;
        _repository = repository;
    }

    /// <summary>
    /// 获取角色的所有活动计划
    /// </summary>
    [HttpGet("character/{characterId:guid}")]
    public async Task<ActionResult<List<ActivityPlan>>> GetByCharacter(Guid characterId, CancellationToken ct)
    {
        var plans = await _repository.GetByCharacterAsync(characterId, ct);
        return Ok(plans);
    }

    /// <summary>
    /// 获取角色指定槽位的活动计划队列
    /// </summary>
    [HttpGet("character/{characterId:guid}/slot/{slotIndex:int}")]
    public async Task<ActionResult<List<ActivityPlan>>> GetBySlot(Guid characterId, int slotIndex, CancellationToken ct)
    {
        if (slotIndex < 0 || slotIndex >= 5)
            return BadRequest("SlotIndex must be between 0 and 4");

        var plans = await _repository.GetByCharacterAndSlotAsync(characterId, slotIndex, ct);
        return Ok(plans);
    }

    /// <summary>
    /// 获取单个活动计划
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActivityPlan>> Get(Guid id, CancellationToken ct)
    {
        var plan = await _repository.GetAsync(id, ct);
        if (plan is null)
            return NotFound();

        return Ok(plan);
    }

    /// <summary>
    /// 创建战斗活动计划
    /// </summary>
    [HttpPost("combat")]
    public async Task<ActionResult<ActivityPlan>> CreateCombat(
        [FromQuery] Guid characterId,
        [FromQuery] int slotIndex = 0,
        [FromQuery] string limitType = "duration",
        [FromQuery] double? limitValue = null,
        [FromQuery] string? enemyId = null,
        [FromQuery] int enemyCount = 1,
        [FromQuery] double? respawnDelay = null,
        [FromQuery] ulong? seed = null,
        CancellationToken ct = default)
    {
        var parsedLimitType = limitType.ToLowerInvariant() switch
        {
            "infinite" => LimitType.Infinite,
            _ => LimitType.Duration
        };

        if (parsedLimitType == LimitType.Duration && !limitValue.HasValue)
            return BadRequest("limitValue is required for duration limit type");

        var payload = new CombatActivityPayload
        {
            EnemyId = enemyId,
            EnemyCount = enemyCount,
            RespawnDelay = respawnDelay,
            Seed = seed
        };

        var payloadJson = JsonSerializer.Serialize(payload);

        try
        {
            var plan = await _service.CreatePlanAsync(
                characterId,
                slotIndex,
                ActivityType.Combat,
                parsedLimitType,
                limitValue,
                payloadJson,
                ct);

            return CreatedAtAction(nameof(Get), new { id = plan.Id }, plan);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 创建地下城活动计划
    /// </summary>
    [HttpPost("dungeon")]
    public async Task<ActionResult<ActivityPlan>> CreateDungeon(
        [FromQuery] Guid characterId,
        [FromQuery] int slotIndex = 0,
        [FromQuery] string limitType = "duration",
        [FromQuery] double? limitValue = null,
        [FromQuery] string dungeonId = "intro_cave",
        [FromQuery] bool loop = false,
        [FromQuery] double? waveDelay = null,
        [FromQuery] double? runDelay = null,
        [FromQuery] ulong? seed = null,
        CancellationToken ct = default)
    {
        var parsedLimitType = limitType.ToLowerInvariant() switch
        {
            "infinite" => LimitType.Infinite,
            _ => LimitType.Duration
        };

        if (parsedLimitType == LimitType.Duration && !limitValue.HasValue)
            return BadRequest("limitValue is required for duration limit type");

        var payload = new DungeonActivityPayload
        {
            DungeonId = dungeonId,
            Loop = loop,
            WaveDelay = waveDelay,
            RunDelay = runDelay,
            Seed = seed
        };

        var payloadJson = JsonSerializer.Serialize(payload);

        try
        {
            var plan = await _service.CreatePlanAsync(
                characterId,
                slotIndex,
                ActivityType.Dungeon,
                parsedLimitType,
                limitValue,
                payloadJson,
                ct);

            return CreatedAtAction(nameof(Get), new { id = plan.Id }, plan);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 启动活动计划（支持启动新计划和恢复暂停的计划）
    /// </summary>
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        try
        {
            var battleId = await _service.StartPlanAsync(id, ct);
            return Ok(new { planId = id, battleId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 恢复暂停的活动计划（等同于 start 端点，提供更清晰的语义）
    /// </summary>
    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        try
        {
            var battleId = await _service.StartPlanAsync(id, ct);
            return Ok(new { planId = id, battleId, resumed = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 暂停活动计划（用于手动暂停，离线自动暂停由后台服务处理）
    /// </summary>
    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct)
    {
        var result = await _service.PausePlanAsync(id, ct);
        if (!result)
            return NotFound();

        return Ok(new { planId = id, paused = true });
    }

    /// <summary>
    /// 停止活动计划
    /// </summary>
    [HttpPost("{id:guid}/stop")]
    public async Task<IActionResult> Stop(Guid id, CancellationToken ct)
    {
        var result = await _service.StopPlanAsync(id, ct);
        if (!result)
            return NotFound();

        return Ok(new { planId = id, stopped = true });
    }

    /// <summary>
    /// 取消活动计划
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _service.CancelPlanAsync(id, ct);
        if (!result)
            return NotFound();

        return Ok(new { planId = id, cancelled = true });
    }

    /// <summary>
    /// 删除活动计划（仅限未启动或已完成的计划）
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var plan = await _repository.GetAsync(id, ct);
        if (plan is null)
            return NotFound();

        if (plan.State == ActivityState.Running || plan.State == ActivityState.Paused)
            return BadRequest("Cannot delete a running or paused plan. Stop it first.");

        await _repository.DeleteAsync(id, ct);
        return NoContent();
    }
}
