using BlazorIdle.Server.Application.Auth;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly GameDbContext _db;

    public UsersController(GameDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<object>> GetCurrentUser()
    {
        var userId = JwtTokenService.GetUserIdFromClaims(User);
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _db.Users
            .Include(u => u.Characters)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.CreatedAt,
            user.LastLoginAt,
            Characters = user.Characters.Select(c => new
            {
                c.Id,
                c.Name,
                c.Level,
                c.Profession
            })
        });
    }

    /// <summary>
    /// 获取指定用户信息
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> GetUser(Guid id)
    {
        var user = await _db.Users
            .Include(u => u.Characters)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.CreatedAt,
            user.LastLoginAt,
            Characters = user.Characters.Select(c => new
            {
                c.Id,
                c.Name,
                c.Level,
                c.Profession
            })
        });
    }

    /// <summary>
    /// 获取用户的所有角色
    /// </summary>
    [HttpGet("{id:guid}/characters")]
    public async Task<ActionResult<List<object>>> GetUserCharacters(Guid id)
    {
        var characters = await _db.Characters
            .Where(c => c.UserId == id)
            .OrderBy(c => c.RosterOrder)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Level,
                c.Profession,
                c.RosterOrder,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(characters);
    }

    /// <summary>
    /// 更新用户信息
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserDto dto)
    {
        var currentUserId = JwtTokenService.GetUserIdFromClaims(User);
        if (currentUserId == null || currentUserId.Value != id)
        {
            return Forbid();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        // 更新邮箱（如果提供且不同）
        if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
        {
            // 检查邮箱是否已被其他用户使用
            if (await _db.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id))
            {
                return BadRequest(new { message = "邮箱已被其他用户使用" });
            }
            user.Email = dto.Email;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "用户信息更新成功" });
    }
}

public record UpdateUserDto(string? Email);
