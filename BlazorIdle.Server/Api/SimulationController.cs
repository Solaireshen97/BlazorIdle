using BlazorIdle.Server.Application.Battles.Simulation;
using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 战斗模拟控制器
/// </summary>
/// <remarks>
/// 提供批量战斗模拟功能，用于预测和分析战斗结果。
/// 
/// <strong>核心功能</strong>：
/// - 批量战斗模拟 - 快速模拟大量战斗获取统计数据
/// 
/// <strong>模拟模式</strong>：
/// - Kills: 击杀数模式 - 模拟直到击杀指定数量的敌人
/// - Hours: 时长模式 - 模拟指定时长的战斗
/// 
/// <strong>用途</strong>：
/// - 装备对比分析（对比不同装备的DPS）
/// - 战斗效率预测（估算击杀速度和收益）
/// - 数值平衡测试
/// - 职业/技能配置优化
/// 
/// <strong>性能特点</strong>：
/// - 使用快速模拟算法
/// - 跳过动画和视觉效果
/// - 只计算核心数值
/// - 可处理大量模拟
/// 
/// <strong>注意事项</strong>：
/// - 模拟结果不会影响角色实际状态
/// - 仅用于数据分析和预测
/// - 大量模拟可能耗时较长
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    private readonly BatchSimulator _sim;

    public SimulationController(BatchSimulator sim)
    {
        _sim = sim;
    }

    /// <summary>
    /// 执行批量战斗模拟
    /// </summary>
    /// <param name="req">模拟请求参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>模拟结果统计</returns>
    /// <response code="200">成功返回模拟结果</response>
    /// <response code="400">请求参数无效</response>
    /// <remarks>
    /// POST /api/simulation
    /// 
    /// 执行批量战斗模拟，返回统计分析结果。
    /// 
    /// <strong>请求体结构</strong>：
    /// ```json
    /// {
    ///   "characterId": "550e8400-e29b-41d4-a716-446655440000",
    ///   "enemyId": "goblin",
    ///   "enemyCount": 1,
    ///   "mode": "Kills",
    ///   "value": 100,
    ///   "sampleSeconds": 10,
    ///   "seed": 12345
    /// }
    /// ```
    /// 
    /// <strong>参数说明</strong>：
    /// - characterId: 角色ID（必填）
    /// - enemyId: 敌人ID，null或空字符串则使用"dummy"（默认敌人）
    /// - enemyCount: 敌人数量（默认1）
    /// - mode: 模拟模式
    ///   - "Kills": 击杀数模式 - 模拟直到击杀value个敌人
    ///   - "Hours": 时长模式 - 模拟value小时的战斗
    /// - value: 目标值（击杀数或小时数，必须>0）
    /// - sampleSeconds: 采样间隔（秒）- 用于生成时间序列数据
    /// - seed: 随机种子（可选）- 用于可重复的模拟结果
    /// 
    /// <strong>返回内容</strong>：
    /// - 总战斗时长
    /// - 总击杀数
    /// - 总伤害
    /// - 平均每秒伤害（DPS）
    /// - 平均击杀时间
    /// - 时间序列数据（按采样间隔）
    /// 
    /// <strong>使用场景</strong>：
    /// 1. 装备对比
    /// ```json
    /// POST /api/simulation
    /// { "characterId": "...", "mode": "Kills", "value": 100 }
    /// ```
    /// 比较不同装备配置下击杀100个敌人的时间
    /// 
    /// 2. 收益预测
    /// ```json
    /// POST /api/simulation
    /// { "characterId": "...", "mode": "Hours", "value": 2 }
    /// ```
    /// 预测2小时挂机能击杀多少敌人、获得多少经验
    /// 
    /// 3. 可重复测试
    /// ```json
    /// POST /api/simulation
    /// { "characterId": "...", "mode": "Kills", "value": 50, "seed": 12345 }
    /// ```
    /// 使用固定种子确保每次模拟结果相同
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<SimulateResponse>> Simulate([FromBody] SimulateRequest req, CancellationToken ct)
    {
        if (req is null) return BadRequest();
        if (string.IsNullOrWhiteSpace(req.EnemyId)) req.EnemyId = "dummy";
        if (req.Value <= 0) return BadRequest("Value must be positive.");
        var result = await _sim.SimulateAsync(req, ct);
        return Ok(result);
    }
}