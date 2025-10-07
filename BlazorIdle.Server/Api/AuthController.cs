using BlazorIdle.Server.Application.Auth;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly GameDbContext _db;
    private readonly JwtTokenService _jwtService;

    public AuthController(GameDbContext db, JwtTokenService jwtService)
    {
        _db = db;
        _jwtService = jwtService;
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        // 验证用户名是否已存在
        if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
        {
            return BadRequest(new { message = "用户名已存在" });
        }

        // 验证邮箱是否已存在
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
        {
            return BadRequest(new { message = "邮箱已被注册" });
        }

        // 创建新用户
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // 生成 JWT Token
        var token = _jwtService.GenerateToken(user.Id, user.Username, user.Email);

        return Ok(new AuthResponseDto
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email
        });
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        // 支持用户名或邮箱登录
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == dto.UsernameOrEmail || u.Email == dto.UsernameOrEmail);

        if (user == null)
        {
            return Unauthorized(new { message = "用户名或密码错误" });
        }

        // 验证密码
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "用户名或密码错误" });
        }

        // 更新最后登录时间
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // 生成 JWT Token
        var token = _jwtService.GenerateToken(user.Id, user.Username, user.Email);

        return Ok(new AuthResponseDto
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email
        });
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);

        if (user == null)
        {
            return NotFound(new { message = "用户不存在" });
        }

        // 验证旧密码
        if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "旧密码错误" });
        }

        // 更新密码
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "密码修改成功" });
    }
}

// DTOs
public record RegisterDto(string Username, string Email, string Password);
public record LoginDto(string UsernameOrEmail, string Password);
public record ChangePasswordDto(string Username, string OldPassword, string NewPassword);
public record AuthResponseDto
{
    public string Token { get; set; } = "";
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
}
