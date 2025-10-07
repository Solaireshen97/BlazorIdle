using BlazorIdle.Server.Application.Auth;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/[controller]")]
public class CharactersController : ControllerBase
{
    private readonly GameDbContext _db;

    public CharactersController(GameDbContext db) => _db = db;

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateCharacterDto dto)
    {
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
            Stamina = sta
        };

        // 如果用户已认证，自动绑定角色到用户
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = JwtTokenService.GetUserIdFromClaims(User);
            if (userId != null)
            {
                c.UserId = userId.Value;
                // 设置 RosterOrder 为用户当前角色数量
                var characterCount = await _db.Characters.CountAsync(ch => ch.UserId == userId.Value);
                c.RosterOrder = characterCount;
            }
        }

        _db.Characters.Add(c);
        await _db.SaveChangesAsync();
        return Ok(new { c.Id, c.Name });
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