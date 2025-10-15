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

/// <summary>
/// 活动计划管理控制器
/// </summary>
/// <remarks>
/// 提供活动计划的完整生命周期管理功能：
/// 
/// <strong>核心功能</strong>：
/// 1. 查询管理 - 获取角色的活动计划、按槽位查询
/// 2. 创建计划 - 战斗计划、地下城计划
/// 3. 状态控制 - 启动、暂停、恢复、停止、取消
/// 4. 计划删除 - 删除未启动或已完成的计划
/// 
/// <strong>活动类型</strong>：
/// - Combat - 普通战斗（持续挂机刷怪）
/// - Dungeon - 地下城副本（多波次战斗）
/// 
/// <strong>限制类型</strong>：
/// - Duration - 时长限制（秒）
/// - Infinite - 无限制（持续到手动停止）
/// 
/// <strong>槽位系统</strong>：
/// - 每个角色有 5 个活动槽位（slotIndex: 0-4）
/// - 每个槽位可以有多个计划排队
/// - 同一槽位的计划按顺序执行
/// 
/// <strong>状态机</strong>：
/// Pending（待开始） → Running（运行中） ⇄ Paused（暂停） → Completed/Stopped/Cancelled（结束）
/// 
/// <strong>自动暂停机制</strong>：
/// - 角色离线时自动暂停正在运行的计划
/// - 角色上线后可手动恢复
/// </remarks>
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
    /// <param name="characterId">角色ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>角色的所有活动计划列表（包含所有槽位和所有状态）</returns>
    /// <response code="200">成功返回活动计划列表</response>
    /// <remarks>
    /// GET /api/activity-plans/character/{characterId}
    /// 
    /// 返回该角色所有槽位（0-4）的所有活动计划，包括：
    /// - Pending - 待开始
    /// - Running - 运行中
    /// - Paused - 暂停
    /// - Completed - 已完成
    /// - Stopped - 已停止
    /// - Cancelled - 已取消
    /// 
    /// 示例请求：
    /// ```
    /// GET /api/activity-plans/character/550e8400-e29b-41d4-a716-446655440000
    /// ```
    /// </remarks>
    [HttpGet("character/{characterId:guid}")]
    public async Task<ActionResult<List<ActivityPlan>>> GetByCharacter(Guid characterId, CancellationToken ct)
    {
        var plans = await _repository.GetByCharacterAsync(characterId, ct);
        return Ok(plans);
    }

    /// <summary>
    /// 获取角色指定槽位的活动计划队列
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="slotIndex">槽位索引（0-4）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>指定槽位的活动计划队列</returns>
    /// <response code="200">成功返回活动计划队列</response>
    /// <response code="400">槽位索引无效（必须在0-4之间）</response>
    /// <remarks>
    /// GET /api/activity-plans/character/{characterId}/slot/{slotIndex}
    /// 
    /// 返回指定槽位的所有活动计划（按创建时间排序）。
    /// 
    /// 槽位规则：
    /// - 每个槽位最多只有一个 Running 状态的计划
    /// - 可以有多个 Pending 状态的计划排队
    /// - 当前计划完成后自动启动下一个 Pending 计划
    /// 
    /// 示例请求：
    /// ```
    /// GET /api/activity-plans/character/550e8400-e29b-41d4-a716-446655440000/slot/0
    /// ```
    /// </remarks>
    [HttpGet("character/{characterId:guid}/slot/{slotIndex:int}")]
    public async Task<ActionResult<List<ActivityPlan>>> GetBySlot(Guid characterId, int slotIndex, CancellationToken ct)
    {
        if (slotIndex < 0 || slotIndex >= 5)
            return BadRequest("SlotIndex must be between 0 and 4");

        var plans = await _repository.GetByCharacterAndSlotAsync(characterId, slotIndex, ct);
        return Ok(plans);
    }

    /// <summary>
    /// 获取单个活动计划详情
    /// </summary>
    /// <param name="id">活动计划ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>活动计划详细信息</returns>
    /// <response code="200">成功返回活动计划</response>
    /// <response code="404">活动计划不存在</response>
    /// <remarks>
    /// GET /api/activity-plans/{id}
    /// 
    /// 返回活动计划的完整信息，包括：
    /// - 基本信息（ID、类型、状态）
    /// - 槽位信息
    /// - 限制配置（类型、剩余时间）
    /// - 负载数据（敌人配置、副本配置等）
    /// - 关联的战斗ID（如果已启动）
    /// 
    /// 示例请求：
    /// ```
    /// GET /api/activity-plans/650e8400-e29b-41d4-a716-446655440001
    /// ```
    /// </remarks>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActivityPlan>> Get(Guid id, CancellationToken ct)
    {
        var plan = await _repository.GetAsync(id, ct);
        if (plan is null)
            return NotFound();

        return Ok(plan);
    }

    /// <summary>
    /// 创建战斗活动计划（普通挂机战斗）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="slotIndex">槽位索引（0-4），默认0</param>
    /// <param name="limitType">限制类型：duration（时长限制）或 infinite（无限制），默认duration</param>
    /// <param name="limitValue">限制值（秒），duration类型时必填</param>
    /// <param name="enemyId">敌人ID，null则随机选择当前区域敌人</param>
    /// <param name="enemyCount">敌人数量，默认1</param>
    /// <param name="respawnDelay">敌人重生延迟（秒），null使用默认配置</param>
    /// <param name="seed">随机种子，null则自动生成</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>创建的活动计划</returns>
    /// <response code="201">成功创建活动计划</response>
    /// <response code="400">参数错误或角色状态不允许创建</response>
    /// <remarks>
    /// POST /api/activity-plans/combat?characterId={id}&amp;slotIndex=0&amp;limitType=duration&amp;limitValue=3600
    /// 
    /// 创建一个普通战斗活动计划，用于持续挂机刷怪。
    /// 
    /// <strong>战斗模式</strong>：
    /// - 重复战斗指定的敌人
    /// - 敌人死亡后按 respawnDelay 延迟重生
    /// - 持续到达到限制或手动停止
    /// 
    /// <strong>限制类型说明</strong>：
    /// - duration: 时长限制，需要提供 limitValue（秒）
    /// - infinite: 无限制，持续到手动停止
    /// 
    /// <strong>敌人选择</strong>：
    /// - 指定 enemyId: 战斗固定敌人
    /// - 不指定: 从当前区域敌人池随机选择
    /// 
    /// 示例请求：
    /// ```
    /// POST /api/activity-plans/combat?characterId=550e8400-e29b-41d4-a716-446655440000&amp;slotIndex=0&amp;limitType=duration&amp;limitValue=3600&amp;enemyId=goblin&amp;enemyCount=3
    /// ```
    /// 
    /// 创建一个1小时的战斗计划，战斗3只哥布林。
    /// </remarks>
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
    /// 创建地下城活动计划（多波次副本战斗）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="slotIndex">槽位索引（0-4），默认0</param>
    /// <param name="limitType">限制类型：duration（时长限制）或 infinite（无限制），默认duration</param>
    /// <param name="limitValue">限制值（秒），duration类型时必填</param>
    /// <param name="dungeonId">地下城ID，默认intro_cave（入门洞穴）</param>
    /// <param name="loop">是否循环副本（完成后重新开始），默认false</param>
    /// <param name="waveDelay">波次间延迟（秒），null使用默认配置</param>
    /// <param name="runDelay">副本间延迟（秒，仅loop=true时有效），null使用默认配置</param>
    /// <param name="seed">随机种子，null则自动生成</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>创建的活动计划</returns>
    /// <response code="201">成功创建活动计划</response>
    /// <response code="400">参数错误或角色状态不允许创建</response>
    /// <remarks>
    /// POST /api/activity-plans/dungeon?characterId={id}&amp;slotIndex=0&amp;limitType=duration&amp;limitValue=7200&amp;dungeonId=intro_cave&amp;loop=true
    /// 
    /// 创建一个地下城活动计划，用于多波次副本挑战。
    /// 
    /// <strong>地下城模式</strong>：
    /// - 按预定义的波次顺序战斗
    /// - 每波次可能有不同的敌人组合
    /// - 完成所有波次后发放副本奖励
    /// 
    /// <strong>循环模式</strong>：
    /// - loop=false: 完成一次副本后结束
    /// - loop=true: 完成后等待 runDelay，然后重新开始
    /// 
    /// <strong>延迟配置</strong>：
    /// - waveDelay: 击败一波敌人后，下一波开始前的等待时间
    /// - runDelay: 完成整个副本后，重新开始前的等待时间（仅循环模式）
    /// 
    /// 示例请求：
    /// ```
    /// POST /api/activity-plans/dungeon?characterId=550e8400-e29b-41d4-a716-446655440000&amp;slotIndex=0&amp;limitType=infinite&amp;dungeonId=intro_cave&amp;loop=true&amp;waveDelay=5&amp;runDelay=10
    /// ```
    /// 
    /// 创建无限循环的入门洞穴副本，波次间延迟5秒，副本间延迟10秒。
    /// </remarks>
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
    /// <param name="id">活动计划ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含计划ID和战斗ID的响应</returns>
    /// <response code="200">成功启动活动计划</response>
    /// <response code="400">活动计划状态不允许启动</response>
    /// <remarks>
    /// POST /api/activity-plans/{id}/start
    /// 
    /// 启动一个活动计划，适用于以下状态：
    /// - Pending: 首次启动
    /// - Paused: 恢复暂停的计划
    /// 
    /// 启动流程：
    /// 1. 检查计划状态（必须是 Pending 或 Paused）
    /// 2. 检查槽位（该槽位不能有其他 Running 状态的计划）
    /// 3. 创建战斗实例
    /// 4. 更新计划状态为 Running
    /// 5. 返回战斗ID供客户端订阅
    /// 
    /// 示例请求：
    /// ```
    /// POST /api/activity-plans/650e8400-e29b-41d4-a716-446655440001/start
    /// ```
    /// 
    /// 响应示例：
    /// ```json
    /// {
    ///   "planId": "650e8400-e29b-41d4-a716-446655440001",
    ///   "battleId": "750e8400-e29b-41d4-a716-446655440002"
    /// }
    /// ```
    /// </remarks>
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
    /// <param name="id">活动计划ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含计划ID、战斗ID和恢复标记的响应</returns>
    /// <response code="200">成功恢复活动计划</response>
    /// <response code="400">活动计划状态不允许恢复</response>
    /// <remarks>
    /// POST /api/activity-plans/{id}/resume
    /// 
    /// 专门用于恢复暂停的活动计划，功能与 /start 相同，但语义更明确。
    /// 
    /// 适用场景：
    /// - 角色离线自动暂停后，上线恢复
    /// - 手动暂停后想要继续
    /// 
    /// 示例请求：
    /// ```
    /// POST /api/activity-plans/650e8400-e29b-41d4-a716-446655440001/resume
    /// ```
    /// 
    /// 响应示例：
    /// ```json
    /// {
    ///   "planId": "650e8400-e29b-41d4-a716-446655440001",
    ///   "battleId": "750e8400-e29b-41d4-a716-446655440002",
    ///   "resumed": true
    /// }
    /// ```
    /// </remarks>
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
    /// <param name="id">活动计划ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含计划ID和暂停标记的响应</returns>
    /// <response code="200">成功暂停活动计划</response>
    /// <response code="404">活动计划不存在</response>
    /// <remarks>
    /// POST /api/activity-plans/{id}/pause
    /// 
    /// 暂停一个正在运行的活动计划。
    /// 
    /// <strong>暂停效果</strong>：
    /// - 停止战斗更新
    /// - 保留当前进度（已使用时长、当前波次等）
    /// - 可以通过 /start 或 /resume 恢复
    /// 
    /// <strong>自动暂停</strong>：
    /// 以下情况会自动暂停计划：
    /// - 角色离线（超过心跳超时时间）
    /// - 系统会在角色离线时自动调用此端点
    /// 
    /// <strong>注意事项</strong>：
    /// - 只能暂停 Running 状态的计划
    /// - 暂停不会影响已获得的奖励
    /// - 暂停后限制时间不会继续消耗
    /// 
    /// 示例请求：
    /// ```
    /// POST /api/activity-plans/650e8400-e29b-41d4-a716-446655440001/pause
    /// ```
    /// </remarks>
    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct)
    {
        var result = await _service.PausePlanAsync(id, ct);
        if (!result)
            return NotFound();

        return Ok(new { planId = id, paused = true });
    }

    /// <summary>
    /// 停止活动计划（正常结束，保留奖励）
    /// </summary>
    /// <param name="id">活动计划ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含计划ID和停止标记的响应</returns>
    /// <response code="200">成功停止活动计划</response>
    /// <response code="404">活动计划不存在</response>
    /// <remarks>
    /// POST /api/activity-plans/{id}/stop
    /// 
    /// 正常停止一个活动计划，适用于想要提前结束但保留已获得奖励的情况。
    /// 
    /// <strong>停止 vs 取消</strong>：
    /// - 停止（Stop）: 正常结束，保留所有已获得的奖励和进度
    /// - 取消（Cancel）: 异常中断，可能会回滚部分内容
    /// 
    /// <strong>停止效果</strong>：
    /// - 结束战斗
    /// - 发放所有累计的奖励
    /// - 将计划状态设置为 Stopped
    /// - 不能再次启动（已结束的计划）
    /// 
    /// <strong>适用场景</strong>：
    /// - 玩家主动选择提前结束
    /// - 已获得足够的资源
    /// - 想要切换到其他活动
    /// 
    /// 示例请求：
    /// ```
    /// POST /api/activity-plans/650e8400-e29b-41d4-a716-446655440001/stop
    /// ```
    /// </remarks>
    [HttpPost("{id:guid}/stop")]
    public async Task<IActionResult> Stop(Guid id, CancellationToken ct)
    {
        var result = await _service.StopPlanAsync(id, ct);
        if (!result)
            return NotFound();

        return Ok(new { planId = id, stopped = true });
    }

    /// <summary>
    /// 取消活动计划（异常中断）
    /// </summary>
    /// <param name="id">活动计划ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含计划ID和取消标记的响应</returns>
    /// <response code="200">成功取消活动计划</response>
    /// <response code="404">活动计划不存在</response>
    /// <remarks>
    /// POST /api/activity-plans/{id}/cancel
    /// 
    /// 取消一个活动计划，用于异常中断的情况。
    /// 
    /// <strong>取消 vs 停止</strong>：
    /// - 取消（Cancel）: 异常中断，不保证奖励完整性
    /// - 停止（Stop）: 正常结束，保留所有奖励
    /// 
    /// <strong>取消效果</strong>：
    /// - 立即结束战斗
    /// - 将计划状态设置为 Cancelled
    /// - 奖励处理取决于实现（可能部分发放或不发放）
    /// 
    /// <strong>适用场景</strong>：
    /// - 系统检测到异常（如装备被删除）
    /// - 玩家在计划排队时想要移除
    /// - 错误创建的计划
    /// 
    /// 示例请求：
    /// ```
    /// POST /api/activity-plans/650e8400-e29b-41d4-a716-446655440001/cancel
    /// ```
    /// </remarks>
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
    /// <param name="id">活动计划ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>无内容响应</returns>
    /// <response code="204">成功删除活动计划</response>
    /// <response code="400">活动计划状态不允许删除（正在运行或暂停中）</response>
    /// <response code="404">活动计划不存在</response>
    /// <remarks>
    /// DELETE /api/activity-plans/{id}
    /// 
    /// 从数据库中永久删除一个活动计划。
    /// 
    /// <strong>删除限制</strong>：
    /// 只能删除以下状态的计划：
    /// - Pending: 未启动的计划
    /// - Completed: 已完成的计划
    /// - Stopped: 已停止的计划
    /// - Cancelled: 已取消的计划
    /// 
    /// 不能删除：
    /// - Running: 正在运行的计划（请先停止）
    /// - Paused: 暂停中的计划（请先停止）
    /// 
    /// <strong>删除效果</strong>：
    /// - 从数据库中永久删除计划记录
    /// - 释放槽位空间
    /// - 无法恢复
    /// 
    /// <strong>使用场景</strong>：
    /// - 清理已完成的计划历史
    /// - 删除排队中不想要的计划
    /// - 整理槽位队列
    /// 
    /// 示例请求：
    /// ```
    /// DELETE /api/activity-plans/650e8400-e29b-41d4-a716-446655440001
    /// ```
    /// </remarks>
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
