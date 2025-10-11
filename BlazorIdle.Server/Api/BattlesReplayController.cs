using Microsoft.AspNetCore.Mvc;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/battles/replay")]
public class BattlesReplayController : ControllerBase
{
    private readonly IBattleRepository _battles;
    private readonly ICharacterRepository _characters;
    private readonly StepBattleCoordinator _coord;
    private readonly Domain.Equipment.Services.EquipmentStatsIntegration _equipmentStatsIntegration;

    public BattlesReplayController(
        IBattleRepository battles, 
        ICharacterRepository characters, 
        StepBattleCoordinator coord,
        Domain.Equipment.Services.EquipmentStatsIntegration equipmentStatsIntegration)
    {
        _battles = battles;
        _characters = characters;
        _coord = coord;
        _equipmentStatsIntegration = equipmentStatsIntegration;
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

        // 构造当前角色的基础+主属性面板（包含装备属性）
        var attrs = new PrimaryAttributes(character.Strength, character.Agility, character.Intellect, character.Stamina);
        
        // 使用 EquipmentStatsIntegration 构建包含装备加成的完整属性
        var stats = await _equipmentStatsIntegration.BuildStatsWithEquipmentAsync(
            record.CharacterId, profession, attrs);

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

    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}