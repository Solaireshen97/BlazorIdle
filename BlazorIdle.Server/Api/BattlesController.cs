using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 战斗相关 API 控制器
/// 责任：协调应用服务( StartBattleService ) 与 仓储( IBattleRepository )，
/// 对外提供：发起战斗、查看战斗摘要、查看战斗分段。
/// 说明：当前属于“瘦控制器+直接调用应用服务/仓储”模式，没有再包装 DTO 类文件（匿名对象直接返回）。
/// </summary>
[ApiController]                       // 启用模型绑定 / 自动 400 / 验证钩子
[Route("api/[controller]")]           // 最终路由前缀: /api/battles
public class BattlesController : ControllerBase
{
    private readonly StartBattleService _startSvc;     // 发起/模拟战斗的应用服务（聚合业务入口）
    private readonly IBattleRepository _battleRepo;    // 读取（含分段）战斗数据的仓储接口

    public BattlesController(StartBattleService startSvc, IBattleRepository battleRepo)
    {
        _startSvc = startSvc;
        _battleRepo = battleRepo;
    }

    /// <summary>
    /// 发起一次战斗模拟（同步完成：即一次性把战斗全过程模拟完并落库）
    /// GET/POST 参数：
    ///   characterId: 要发起战斗的角色 Id
    ///   seconds: 模拟时长（默认 15s）
    /// 返回：创建好的 battleId（前端据此再查询 summary / segments）
    /// 说明：战斗模拟时间是“模拟时钟”而非真实等待；服务端内部立即完成计算。
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<object>> Start([FromQuery] Guid characterId, [FromQuery] double seconds = 15)
    {
        // 此处缺少：参数校验（例如 seconds > 0 且 <= 上限），可在后续增强
        var id = await _startSvc.StartAsync(characterId, seconds);
        return Ok(new { battleId = id });
    }

    /// <summary>
    /// 获取战斗摘要：
    ///   包含：总伤害、持续时间、DPS（简单计算）、分段数量等。
    ///   用途：前端展示一个简要统计。
    /// </summary>
    /// <param name="id">战斗 Id</param>
    [HttpGet("{id:guid}/summary")]
    public async Task<ActionResult<object>> Summary(Guid id)
    {
        // 读取时连同 Segments 一起取出，虽然这里只用 Count；可以考虑分离只读摘要的查询以减少加载
        var battle = await _battleRepo.GetWithSegmentsAsync(id);
        if (battle == null) return NotFound();

        // 防止除 0：用最小值保护
        var dps = battle.TotalDamage / Math.Max(0.0001, battle.DurationSeconds);

        return Ok(new
        {
            battle.Id,
            battle.CharacterId,
            battle.TotalDamage,
            battle.DurationSeconds,
            Dps = Math.Round(dps, 2),
            SegmentCount = battle.Segments.Count,
            battle.AttackIntervalSeconds,
            battle.SpecialIntervalSeconds
        });
    }

    /// <summary>
    /// 获取战斗的所有分段（按开始时间排序）
    /// 每个分段包含：时间窗口、事件数量、伤害、来源伤害字典
    /// </summary>
    /// <param name="id">战斗 Id</param>
    [HttpGet("{id:guid}/segments")]
    public async Task<ActionResult<IEnumerable<object>>> Segments(Guid id)
    {
        var battle = await _battleRepo.GetWithSegmentsAsync(id);
        if (battle == null) return NotFound();

        // 说明：Segments 中的 DamageBySourceJson 是 JSON 字符串，需要反序列化为字典。
        // 这里逐段 Deserialize 可能会有小的分配开销；后续可考虑预解析 / 使用 JsonDocument。
        var result = battle.Segments
            .OrderBy(s => s.StartTime)
            .Select(s => new {
                s.StartTime,
                s.EndTime,
                s.EventCount,
                s.TotalDamage,
                DamageBySource = JsonSerializer.Deserialize<Dictionary<string, int>>(s.DamageBySourceJson) ?? new(),
                ResourceFlow = JsonSerializer.Deserialize<Dictionary<string, int>>(s.ResourceFlowJson) ?? new()
            });

        return Ok(result);
    }
}