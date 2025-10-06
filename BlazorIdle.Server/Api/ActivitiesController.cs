using BlazorIdle.Server.Application.Activities;
using BlazorIdle.Server.Domain.Activity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/activities")]
public class ActivitiesController : ControllerBase
{
    private readonly ActivityCoordinator _coordinator;
    
    public ActivitiesController(ActivityCoordinator coordinator)
    {
        _coordinator = coordinator;
    }
    
    /// <summary>
    /// 创建活动计划
    /// </summary>
    [HttpPost("plans")]
    public ActionResult<ActivityPlanDto> CreatePlan([FromBody] CreateActivityPlanRequest request)
    {
        try
        {
            LimitSpec limit = request.LimitType?.ToLowerInvariant() switch
            {
                "duration" => new DurationLimit(request.LimitValue ?? 60.0),
                "count" => new CountLimit((int)(request.LimitValue ?? 10)),
                "infinite" => new InfiniteLimit(),
                _ => new DurationLimit(request.LimitValue ?? 60.0)
            };
            
            var type = request.Type?.ToLowerInvariant() switch
            {
                "combat" => ActivityType.Combat,
                "gather" => ActivityType.Gather,
                "craft" => ActivityType.Craft,
                _ => ActivityType.Combat
            };
            
            var plan = _coordinator.CreatePlan(
                characterId: request.CharacterId,
                slotIndex: request.SlotIndex,
                type: type,
                limit: limit,
                payloadJson: request.PayloadJson ?? "{}"
            );
            
            return Ok(MapToDto(plan));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    
    /// <summary>
    /// 获取活动计划详情
    /// </summary>
    [HttpGet("plans/{planId:guid}")]
    public ActionResult<ActivityPlanDto> GetPlan(Guid planId)
    {
        var plan = _coordinator.GetPlan(planId);
        if (plan is null)
            return NotFound();
        
        return Ok(MapToDto(plan));
    }
    
    /// <summary>
    /// 获取角色的所有槽位信息
    /// </summary>
    [HttpGet("characters/{characterId:guid}/slots")]
    public ActionResult<List<ActivitySlotDto>> GetCharacterSlots(Guid characterId)
    {
        var slots = _coordinator.GetCharacterSlots(characterId);
        var dtos = new List<ActivitySlotDto>();
        
        foreach (var slot in slots)
        {
            var (current, queued) = _coordinator.GetSlotPlans(characterId, slot.SlotIndex);
            
            dtos.Add(new ActivitySlotDto
            {
                SlotIndex = slot.SlotIndex,
                CharacterId = characterId,
                CurrentPlan = current is not null ? MapToDto(current) : null,
                QueuedPlans = queued.Select(MapToDto).ToList()
            });
        }
        
        return Ok(dtos);
    }
    
    /// <summary>
    /// 取消活动计划
    /// </summary>
    [HttpPost("plans/{planId:guid}/cancel")]
    public async Task<IActionResult> CancelPlan(Guid planId, CancellationToken ct)
    {
        var success = await _coordinator.CancelPlanAsync(planId, ct);
        if (!success)
            return NotFound();
        
        return Ok(new { success = true });
    }
    
    private static ActivityPlanDto MapToDto(ActivityPlan plan)
    {
        return new ActivityPlanDto
        {
            Id = plan.Id,
            CharacterId = plan.CharacterId,
            SlotIndex = plan.SlotIndex,
            Type = plan.Type.ToString().ToLowerInvariant(),
            State = plan.State.ToString().ToLowerInvariant(),
            LimitType = plan.Limit.GetLimitType().ToLowerInvariant(),
            PayloadJson = plan.PayloadJson,
            CreatedAtUtc = plan.CreatedAtUtc,
            StartedAtUtc = plan.StartedAtUtc,
            EndedAtUtc = plan.EndedAtUtc,
            Progress = new ActivityProgressDto
            {
                SimulatedSeconds = plan.Progress.SimulatedSeconds,
                CompletedCount = plan.Progress.CompletedCount
            }
        };
    }
}

public sealed class CreateActivityPlanRequest
{
    public Guid CharacterId { get; set; }
    public int SlotIndex { get; set; }
    public string? Type { get; set; } // "combat" | "gather" | "craft"
    public string? LimitType { get; set; } // "duration" | "count" | "infinite"
    public double? LimitValue { get; set; } // 时长（秒）或计数
    public string? PayloadJson { get; set; } // JSON格式的活动特定参数
}

public sealed class ActivityPlanDto
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public int SlotIndex { get; set; }
    public string Type { get; set; } = "combat";
    public string State { get; set; } = "pending";
    public string LimitType { get; set; } = "duration";
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public ActivityProgressDto Progress { get; set; } = new();
}

public sealed class ActivityProgressDto
{
    public double SimulatedSeconds { get; set; }
    public int CompletedCount { get; set; }
}

public sealed class ActivitySlotDto
{
    public int SlotIndex { get; set; }
    public Guid CharacterId { get; set; }
    public ActivityPlanDto? CurrentPlan { get; set; }
    public List<ActivityPlanDto> QueuedPlans { get; set; } = new();
}
