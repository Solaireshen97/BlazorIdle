using BlazorIdle.Server.Application.Auth;
using BlazorIdle.Server.Application.Battles.Offline;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/[controller]")]
public class CharactersController : ControllerBase
{
    private readonly GameDbContext _db;
    private readonly OfflineSettlementService _offlineService;
    private readonly IConfiguration _configuration;

    public CharactersController(
        GameDbContext db,
        OfflineSettlementService offlineService,
        IConfiguration configuration)
    {
        _db = db;
        _offlineService = offlineService;
        _configuration = configuration;
    }

    [HttpPost]
    [Authorize]  // 现在要求用户必须登录才能创建角色
    public async Task<ActionResult<object>> Create([FromBody] CreateCharacterDto dto)
    {
        // 获取当前用户ID
        var userId = JwtTokenService.GetUserIdFromClaims(User);
        if (userId == null)
        {
            return Unauthorized(new { message = "请先登录" });
        }

        // 检查用户角色数量限制（最多5个）
        var characterCount = await _db.Characters.CountAsync(ch => ch.UserId == userId.Value);
        if (characterCount >= 5)
        {
            return BadRequest(new { message = "已达到角色数量上限（最多5个角色）" });
        }

        var (str, agi, intel, sta) = DefaultAttributesFor(dto.Profession);
        // 覆盖默认值（若 DTO 提供）
        str = dto.Strength ?? str;
        agi = dto.Agility ?? agi;
        intel = dto.Intellect ?? intel;
        sta = dto.Stamina ?? sta;

        var c = new Character
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Profession = dto.Profession,
            Level = 1,
            Strength = str,
            Agility = agi,
            Intellect = intel,
            Stamina = sta,
            UserId = userId.Value,
            RosterOrder = characterCount  // 使用当前数量作为顺序
        };

        _db.Characters.Add(c);
        await _db.SaveChangesAsync();
        return Ok(new { c.Id, c.Name, c.RosterOrder });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id)
    {
        var c = await _db.Characters.FirstOrDefaultAsync(x => x.Id == id);
        return c is null
            ? NotFound()
            : Ok(new { c.Id, c.Name, c.Level, c.Profession, c.Strength, c.Agility, c.Intellect, c.Stamina });
    }

    /// <summary>
    /// 将角色绑定到用户（需要认证）
    /// </summary>
    [HttpPut("{id:guid}/bind-user")]
    [Authorize]
    public async Task<IActionResult> BindToUser(Guid id)
    {
        var userId = JwtTokenService.GetUserIdFromClaims(User);
        if (userId == null)
        {
            return Unauthorized();
        }

        var character = await _db.Characters.FirstOrDefaultAsync(c => c.Id == id);
        if (character == null)
        {
            return NotFound(new { message = "角色不存在" });
        }

        if (character.UserId != null)
        {
            return BadRequest(new { message = "角色已绑定到其他用户" });
        }

        character.UserId = userId.Value;
        // 设置 RosterOrder 为用户当前角色数量
        var characterCount = await _db.Characters.CountAsync(c => c.UserId == userId.Value);
        character.RosterOrder = characterCount;

        await _db.SaveChangesAsync();
        return Ok(new { message = "角色绑定成功" });
    }

    /// <summary>
    /// 调整角色在 Roster 中的顺序（需要认证）
    /// </summary>
    [HttpPut("{id:guid}/reorder")]
    [Authorize]
    public async Task<IActionResult> ReorderCharacter(Guid id, [FromBody] ReorderDto dto)
    {
        var userId = JwtTokenService.GetUserIdFromClaims(User);
        if (userId == null)
        {
            return Unauthorized();
        }

        var character = await _db.Characters.FirstOrDefaultAsync(c => c.Id == id);
        if (character == null)
        {
            return NotFound(new { message = "角色不存在" });
        }

        if (character.UserId != userId.Value)
        {
            return Forbid();
        }

        character.RosterOrder = dto.RosterOrder;
        await _db.SaveChangesAsync();
        return Ok(new { message = "角色顺序调整成功" });
    }

    /// <summary>
    /// 更新角色心跳时间，标记角色在线
    /// 自动检测离线状态并触发离线结算（如果需要）
    /// POST /api/characters/{id}/heartbeat
    /// </summary>
    [HttpPost("{id:guid}/heartbeat")]
    public async Task<IActionResult> Heartbeat(Guid id)
    {
        var character = await _db.Characters.FirstOrDefaultAsync(c => c.Id == id);
        if (character == null)
        {
            return NotFound(new { message = "角色不存在" });
        }

        // 获取离线检测阈值（默认60秒）
        var offlineThresholdSeconds = _configuration.GetValue<int>("Offline:OfflineDetectionSeconds", 60);
        var autoApplyRewards = _configuration.GetValue<bool>("Offline:AutoApplyRewards", true);
        
        // 检查是否需要触发离线结算
        OfflineCheckResult? offlineResult = null;
        if (character.LastSeenAtUtc.HasValue)
        {
            var offlineSeconds = (DateTime.UtcNow - character.LastSeenAtUtc.Value).TotalSeconds;
            
            // 如果离线时间超过阈值，触发离线结算
            if (offlineSeconds >= offlineThresholdSeconds)
            {
                try
                {
                    // 执行离线检测和结算（但暂不更新心跳，让 CheckAndSettleAsync 处理）
                    offlineResult = await _offlineService.CheckAndSettleAsync(id);
                    
                    // 如果自动应用收益已开启且有结算结果，立即应用
                    if (autoApplyRewards && offlineResult.Settlement != null)
                    {
                        await _offlineService.ApplySettlementAsync(id, offlineResult.Settlement);
                    }
                }
                catch (Exception ex)
                {
                    // 记录错误但不阻止心跳更新
                    // 在生产环境中应该使用适当的日志记录
                    Console.WriteLine($"离线结算失败: {ex.Message}");
                }
            }
        }
        
        // 更新心跳时间（如果 CheckAndSettleAsync 还没更新的话）
        // CheckAndSettleAsync 会更新 LastSeenAtUtc，所以这里需要重新获取
        character = await _db.Characters.FirstOrDefaultAsync(c => c.Id == id);
        if (character != null)
        {
            character.LastSeenAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        
        return Ok(new
        {
            message = "心跳更新成功",
            timestamp = character?.LastSeenAtUtc,
            offlineSettlement = offlineResult
        });
    }

    private static (int str, int agi, int intel, int sta) DefaultAttributesFor(Profession p)
        => p switch
        {
            Profession.Warrior => (20, 10, 5, 15),
            Profession.Ranger => (10, 20, 8, 12),
            _ => (10, 10, 10, 10)
        };
}

// 扩展创建 DTO：支持可选主属性（未传则按职业默认）
public record CreateCharacterDto(
    string Name,
    Profession Profession,
    int? Strength = null,
    int? Agility = null,
    int? Intellect = null,
    int? Stamina = null
);

public record ReorderDto(int RosterOrder);