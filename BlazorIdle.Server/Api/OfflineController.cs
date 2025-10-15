using BlazorIdle.Server.Application.Battles.Offline;
using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 离线结算控制器
/// </summary>
/// <remarks>
/// 提供离线时间补偿和收益结算功能。
/// 
/// <strong>核心功能</strong>：
/// 1. 离线检查 - 检查并计算离线期间的收益
/// 2. 收益应用 - 发放离线收益到角色
/// 3. 离线模拟 - 快速模拟离线期间的战斗和掉落
/// 
/// <strong>工作原理</strong>：
/// 当角色离线时（超过心跳超时），系统会：
/// 1. 自动暂停所有正在运行的活动计划
/// 2. 记录离线开始时间
/// 3. 角色上线时触发离线结算
/// 4. 根据离线时长和活动配置计算收益
/// 5. 自动或手动发放收益
/// 
/// <strong>离线上限</strong>：
/// - 默认最多结算 12 小时离线收益
/// - 超过上限的时间不会获得额外收益
/// - 可通过配置调整上限
/// 
/// <strong>结算模式</strong>：
/// - continuous: 持续战斗模式（单一敌人重复战斗）
/// - dungeon: 地下城模式（多波次副本）
/// 
/// <strong>掉落模式</strong>：
/// - expected: 期望掉落（基于概率的平均值）
/// - sampled: 采样掉落（实际随机掉落）
/// 
/// <strong>自动触发</strong>：
/// 角色心跳（Heartbeat）时自动检查离线时间并触发结算。
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class OfflineController : ControllerBase
{
    private readonly OfflineSettlementService _offline;

    public OfflineController(OfflineSettlementService offline) => _offline = offline;

    /// <summary>
    /// 检查角色离线时间并返回结算结果
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>离线检查结果，包含离线时长和计算的收益</returns>
    /// <response code="200">成功返回离线结算结果</response>
    /// <response code="404">角色不存在</response>
    /// <remarks>
    /// GET /api/offline/check?characterId={id}
    /// 
    /// 检查角色的离线时间并计算应获得的收益。
    /// 
    /// <strong>注意</strong>：
    /// - 从心跳（Heartbeat）自动触发后，此端点主要用于查询已结算的结果
    /// - 不会重复结算同一段离线时间
    /// - 结算结果会缓存，避免重复计算
    /// 
    /// <strong>返回内容</strong>：
    /// - 离线时长（秒）
    /// - 离线开始时间和结束时间
    /// - 计算的金币和经验收益
    /// - 掉落物品列表
    /// - 战斗统计（总伤害、击杀数等）
    /// 
    /// <strong>计算规则</strong>：
    /// 1. 获取角色最后一次在线时间
    /// 2. 计算离线时长（上限12小时）
    /// 3. 根据暂停前的活动计划配置模拟战斗
    /// 4. 使用快速模拟算法计算收益
    /// 5. 返回结算结果（不自动发放）
    /// 
    /// 示例请求：
    /// ```
    /// GET /api/offline/check?characterId=550e8400-e29b-41d4-a716-446655440000
    /// ```
    /// 
    /// 响应示例：
    /// ```json
    /// {
    ///   "offlineSeconds": 7200,
    ///   "gold": 15000,
    ///   "exp": 8500,
    ///   "totalKills": 240,
    ///   "loot": [{"itemId": "iron_ore", "count": 45}]
    /// }
    /// ```
    /// </remarks>
    [HttpGet("check")]
    public async Task<ActionResult<OfflineCheckResult>> CheckOffline(
        [FromQuery] Guid characterId,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _offline.CheckAndSettleAsync(characterId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 应用离线结算，实际发放收益到角色
    /// </summary>
    /// <param name="request">包含角色ID和结算结果的请求体</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>成功消息</returns>
    /// <response code="200">成功发放离线收益</response>
    /// <response code="404">角色不存在</response>
    /// <remarks>
    /// POST /api/offline/apply
    /// 
    /// 将离线结算的收益实际发放到角色。
    /// 
    /// <strong>注意</strong>：
    /// - 在自动应用收益模式下，心跳会自动调用此端点
    /// - 此端点主要作为备用/手动触发选项保留
    /// - 同一结算结果只能应用一次（防止重复发放）
    /// 
    /// <strong>发放流程</strong>：
    /// 1. 验证角色存在
    /// 2. 检查结算结果是否已应用
    /// 3. 增加角色金币和经验
    /// 4. 发放掉落物品到背包
    /// 5. 标记结算结果为已应用
    /// 6. 记录应用时间
    /// 
    /// <strong>幂等性</strong>：
    /// - 重复调用不会重复发放收益
    /// - 通过结算ID追踪是否已应用
    /// 
    /// 示例请求：
    /// ```json
    /// POST /api/offline/apply
    /// {
    ///   "characterId": "550e8400-e29b-41d4-a716-446655440000",
    ///   "settlement": {
    ///     "offlineSeconds": 7200,
    ///     "gold": 15000,
    ///     "exp": 8500,
    ///     "loot": [{"itemId": "iron_ore", "count": 45}]
    ///   }
    /// }
    /// ```
    /// </remarks>
    [HttpPost("apply")]
    public async Task<ActionResult> ApplySettlement(
        [FromBody] ApplySettlementRequest request,
        CancellationToken ct = default)
    {
        try
        {
            await _offline.ApplySettlementAsync(
                request.CharacterId,
                request.Settlement,
                ct);
            return Ok(new { success = true, message = "收益已发放" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 手动模拟离线结算（测试/调试用）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="seconds">模拟的离线时长（秒）</param>
    /// <param name="mode">模拟模式：continuous（持续战斗）或 dungeon（地下城），默认continuous</param>
    /// <param name="enemyId">敌人ID，默认dummy</param>
    /// <param name="enemyCount">敌人数量，默认1</param>
    /// <param name="dungeonId">地下城ID（仅mode=dungeon时有效）</param>
    /// <param name="seed">随机种子，null则自动生成</param>
    /// <param name="dropMode">掉落模式：expected（期望值）或 sampled（实际随机），默认expected</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>模拟结果，包含收益和战斗统计</returns>
    /// <response code="200">成功返回模拟结果</response>
    /// <response code="400">参数错误</response>
    /// <remarks>
    /// POST /api/offline/settle?characterId={id}&amp;seconds=7200&amp;mode=continuous&amp;enemyId=dummy&amp;enemyCount=1&amp;dropMode=sampled
    /// 
    /// 手动触发离线模拟计算，主要用于测试和调试。
    /// 
    /// <strong>模拟模式</strong>：
    /// - continuous: 持续战斗单一敌人
    ///   - 适用于普通挂机场景
    ///   - 需要指定 enemyId 和 enemyCount
    ///   - 敌人死亡后立即重生继续战斗
    /// 
    /// - dungeon: 地下城多波次战斗
    ///   - 适用于副本场景
    ///   - 需要指定 dungeonId
    ///   - 按波次配置顺序战斗
    /// 
    /// <strong>掉落模式</strong>：
    /// - expected: 期望掉落
    ///   - 基于掉落概率计算平均收益
    ///   - 结果稳定，无随机性
    ///   - 推荐用于离线结算
    /// 
    /// - sampled: 采样掉落
    ///   - 实际随机掉落
    ///   - 结果有波动
    ///   - 更真实但可能不公平
    /// 
    /// <strong>性能考虑</strong>：
    /// - 长时间模拟（>2小时）可能耗时较长
    /// - 使用快速模拟算法跳过细节
    /// - 建议 seconds 参数不超过 43200（12小时）
    /// 
    /// <strong>注意事项</strong>：
    /// - 此端点不会实际发放收益到角色
    /// - 仅返回模拟结果供查看
    /// - 不影响角色实际状态
    /// - 主要用于测试和预览
    /// 
    /// 示例请求：
    /// ```
    /// POST /api/offline/settle?characterId=550e8400-e29b-41d4-a716-446655440000&amp;seconds=7200&amp;mode=continuous&amp;enemyId=goblin&amp;enemyCount=3&amp;dropMode=expected
    /// ```
    /// 
    /// 响应示例：
    /// ```json
    /// {
    ///   "characterId": "550e8400-e29b-41d4-a716-446655440000",
    ///   "simulatedSeconds": 7200,
    ///   "totalDamage": 384000,
    ///   "totalKills": 240,
    ///   "mode": "continuous",
    ///   "enemyId": "goblin",
    ///   "enemyCount": 3,
    ///   "dropMode": "expected",
    ///   "gold": 15000,
    ///   "exp": 8500,
    ///   "lootExpected": [{"itemId": "goblin_ear", "count": 72}],
    ///   "lootSampled": null
    /// }
    /// ```
    /// </remarks>
    [HttpPost("settle")]
    public async Task<ActionResult<object>> Settle(
        [FromQuery] Guid characterId,
        [FromQuery] double seconds,
        [FromQuery] string? mode = "continuous",
        [FromQuery] string? enemyId = "dummy",
        [FromQuery] int enemyCount = 1,
        [FromQuery] string? dungeonId = null,
        [FromQuery] ulong? seed = null,
        [FromQuery] string? dropMode = "expected",
        CancellationToken ct = default)
    {
        if (seconds <= 0) return BadRequest("seconds must be positive.");
        var res = await _offline.SimulateAsync(characterId, TimeSpan.FromSeconds(seconds), mode, enemyId, enemyCount, dungeonId, seed, dropMode, ct);
        return Ok(new
        {
            res.CharacterId,
            res.SimulatedSeconds,
            res.TotalDamage,
            res.TotalKills,
            res.Mode,
            res.EnemyId,
            res.EnemyCount,
            res.DungeonId,
            res.DropMode,
            res.Gold,
            res.Exp,
            res.LootExpected,
            res.LootSampled
        });
    }
}

/// <summary>
/// 应用离线结算请求
/// </summary>
/// <param name="CharacterId">角色ID</param>
/// <param name="Settlement">离线快速推进结算结果，包含金币、经验和掉落物品</param>
public record ApplySettlementRequest(
    Guid CharacterId,
    OfflineFastForwardResult Settlement
);