using Microsoft.AspNetCore.Mvc;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 战斗回放控制器
/// </summary>
/// <remarks>
/// 提供基于历史战斗记录的回放和重模拟功能。
/// 
/// <strong>核心功能</strong>：
/// - 从历史战斗记录启动回放
/// 
/// <strong>回放特点</strong>：
/// - 复用历史战斗的配置（种子、敌人、时长）
/// - 使用角色当前属性重新模拟
/// - 可用于对比不同装备/属性的影响
/// 
/// <strong>使用场景</strong>：
/// 1. 装备对比
///    - 记录装备A的战斗
///    - 更换装备B
///    - 回放原战斗，查看差异
/// 
/// 2. 属性测试
///    - 在某个属性配置下战斗
///    - 调整属性
///    - 回放查看属性变化的影响
/// 
/// 3. 随机性验证
///    - 使用固定种子的战斗
///    - 回放验证结果可重复性
/// 
/// <strong>注意事项</strong>：
/// - 回放使用当前角色属性，不是历史属性
/// - 如果职业变化，面板计算会不同
/// - 种子相同时，随机事件应该相同
/// </remarks>
[ApiController]
[Route("api/battles/replay")]
public class BattlesReplayController : ControllerBase
{
    private readonly IBattleRepository _battles;
    private readonly ICharacterRepository _characters;
    private readonly StepBattleCoordinator _coord;

    public BattlesReplayController(IBattleRepository battles, ICharacterRepository characters, StepBattleCoordinator coord)
    {
        _battles = battles;
        _characters = characters;
        _coord = coord;
    }

    // 用历史 BattleRecord 启动一场 Step 回放/重模拟
    // 默认复用历史 seed、敌人、时长；使用“当前角色的职业与主属性→面板”进行重模拟
    // 可通过 seconds 覆盖时长；enemyCount 默认 1
    [HttpPost("{battleId:guid}/start")]
    public async Task<IActionResult> StartFromRecord(Guid battleId, [FromQuery] double? seconds = null, [FromQuery] int enemyCount = 1)
    {
        var record = await _battles.GetWithSegmentsAsync(battleId);
        if (record is null) return NotFound("Battle record not found.");

        var character = await _characters.GetAsync(record.CharacterId);
        if (character is null) return NotFound("Character not found.");

        var profession = character.Profession;

        // 构造当前角色的基础+主属性面板（与普通 Step 启动一致）
        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        var attrs = new PrimaryAttributes(character.Strength, character.Agility, character.Intellect, character.Stamina);
        var derived = StatsBuilder.BuildDerived(profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        // 复用历史 seed（如不合法则派生）
        ulong seed = 0;
        if (!ulong.TryParse(record.Seed, out seed))
        {
            seed = DeriveSeed(record.CharacterId);
        }

        var simSeconds = seconds ?? Math.Max(1.0, record.DurationSeconds);
        var enemyId = record.EnemyId;
        enemyCount = Math.Max(1, enemyCount);

        var stepId = _coord.Start(record.CharacterId, profession, stats, simSeconds, seed, enemyId, enemyCount);
        return Ok(new { battleId = stepId, seed, enemyId, enemyCount, sourceRecordId = record.Id });
    }

    /// <summary>
    /// 根据角色ID生成随机种子
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>生成的随机种子</returns>
    /// <remarks>
    /// 当历史战斗记录的种子无效时，使用此方法生成新种子。
    /// 基于角色ID和当前时间，确保唯一性和随机性。
    /// </remarks>
    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}