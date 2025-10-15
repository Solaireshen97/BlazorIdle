using BlazorIdle.Server.Application.Auth;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 认证系统 API 控制器
/// </summary>
/// <remarks>
/// 提供的功能：
/// - 用户注册（创建新账号）
/// - 用户登录（验证并返回JWT Token）
/// - 修改密码（验证旧密码后更新）
/// 
/// 安全特性：
/// - 使用BCrypt加密存储密码
/// - 登录成功返回JWT Token用于后续认证
/// - 密码验证失败不暴露具体原因
/// - 支持用户名或邮箱登录
/// 
/// 认证要求：
/// - 注册和登录接口无需认证
/// - 修改密码接口当前无需认证（未来可能需要）
/// 
/// 基础路由：/api/auth
/// </remarks>
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
    /// <param name="dto">注册数据，包含用户名、邮箱和密码</param>
    /// <returns>认证响应，包含JWT Token和用户信息</returns>
    /// <response code="200">注册成功，返回Token</response>
    /// <response code="400">注册失败（用户名或邮箱已存在）</response>
    /// <remarks>
    /// 功能说明：
    /// - 创建新用户账号
    /// - 验证用户名和邮箱的唯一性
    /// - 使用BCrypt加密存储密码
    /// - 自动生成JWT Token用于免登录
    /// 
    /// 验证规则：
    /// - 用户名必须唯一
    /// - 邮箱必须唯一
    /// - 密码使用BCrypt加密（不可逆）
    /// 
    /// 注册流程：
    /// 1. 验证用户名是否已存在
    /// 2. 验证邮箱是否已被注册
    /// 3. 创建用户记录（密码加密）
    /// 4. 生成JWT Token
    /// 5. 返回Token和用户信息
    /// 
    /// 请求示例：
    /// <code>
    /// POST /api/auth/register
    /// {
    ///   "username": "player123",
    ///   "email": "player@example.com",
    ///   "password": "SecurePassword123"
    /// }
    /// </code>
    /// 
    /// 响应示例（成功）：
    /// <code>
    /// {
    ///   "token": "eyJhbGciOiJIUzI1NiIs...",
    ///   "userId": "123e4567-e89b-12d3-a456-426614174000",
    ///   "username": "player123",
    ///   "email": "player@example.com"
    /// }
    /// </code>
    /// 
    /// 响应示例（失败）：
    /// <code>
    /// {
    ///   "message": "用户名已存在"
    /// }
    /// </code>
    /// </remarks>
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
    /// <param name="dto">登录数据，包含用户名/邮箱和密码</param>
    /// <returns>认证响应，包含JWT Token和用户信息</returns>
    /// <response code="200">登录成功，返回Token</response>
    /// <response code="401">登录失败（用户不存在或密码错误）</response>
    /// <remarks>
    /// 功能说明：
    /// - 验证用户凭据并生成JWT Token
    /// - 支持使用用户名或邮箱登录
    /// - 使用BCrypt验证密码
    /// - 更新用户的最后登录时间
    /// 
    /// 安全设计：
    /// - 用户不存在和密码错误返回相同错误消息
    /// - 防止通过错误消息探测用户是否存在
    /// - 密码验证使用BCrypt（防止彩虹表攻击）
    /// 
    /// 登录流程：
    /// 1. 根据用户名或邮箱查找用户
    /// 2. 验证密码（使用BCrypt）
    /// 3. 更新最后登录时间
    /// 4. 生成JWT Token
    /// 5. 返回Token和用户信息
    /// 
    /// 请求示例：
    /// <code>
    /// POST /api/auth/login
    /// {
    ///   "usernameOrEmail": "player123",
    ///   "password": "SecurePassword123"
    /// }
    /// </code>
    /// 
    /// 响应示例（成功）：
    /// <code>
    /// {
    ///   "token": "eyJhbGciOiJIUzI1NiIs...",
    ///   "userId": "123e4567-e89b-12d3-a456-426614174000",
    ///   "username": "player123",
    ///   "email": "player@example.com"
    /// }
    /// </code>
    /// 
    /// 响应示例（失败）：
    /// <code>
    /// {
    ///   "message": "用户名或密码错误"
    /// }
    /// </code>
    /// </remarks>
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
    /// <param name="dto">密码修改数据，包含用户名、旧密码和新密码</param>
    /// <returns>修改结果</returns>
    /// <response code="200">密码修改成功</response>
    /// <response code="400">旧密码错误</response>
    /// <response code="404">用户不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 修改用户密码
    /// - 验证旧密码正确性
    /// - 使用BCrypt加密新密码
    /// - 更新UpdatedAt时间戳
    /// 
    /// 安全考虑：
    /// - 必须提供正确的旧密码
    /// - 新密码使用BCrypt加密
    /// - 当前无需认证（未来建议添加JWT验证）
    /// 
    /// 未来改进：
    /// - 添加JWT认证要求
    /// - 验证新密码强度（长度、复杂度）
    /// - 密码修改后可选的强制重新登录
    /// - 发送邮件通知密码已修改
    /// 
    /// 修改流程：
    /// 1. 根据用户名查找用户
    /// 2. 验证旧密码
    /// 3. 加密并保存新密码
    /// 4. 更新时间戳
    /// 
    /// 请求示例：
    /// <code>
    /// POST /api/auth/change-password
    /// {
    ///   "username": "player123",
    ///   "oldPassword": "OldPassword123",
    ///   "newPassword": "NewSecurePassword456"
    /// }
    /// </code>
    /// 
    /// 响应示例（成功）：
    /// <code>
    /// {
    ///   "message": "密码修改成功"
    /// }
    /// </code>
    /// 
    /// 响应示例（失败）：
    /// <code>
    /// {
    ///   "message": "旧密码错误"
    /// }
    /// </code>
    /// </remarks>
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

// 认证相关数据传输对象

/// <summary>
/// 注册请求数据传输对象
/// </summary>
/// <param name="Username">用户名，必须唯一</param>
/// <param name="Email">邮箱地址，必须唯一</param>
/// <param name="Password">密码，将使用BCrypt加密存储</param>
public record RegisterDto(string Username, string Email, string Password);

/// <summary>
/// 登录请求数据传输对象
/// </summary>
/// <param name="UsernameOrEmail">用户名或邮箱，两者皆可</param>
/// <param name="Password">密码</param>
public record LoginDto(string UsernameOrEmail, string Password);

/// <summary>
/// 修改密码请求数据传输对象
/// </summary>
/// <param name="Username">用户名</param>
/// <param name="OldPassword">旧密码</param>
/// <param name="NewPassword">新密码</param>
public record ChangePasswordDto(string Username, string OldPassword, string NewPassword);

/// <summary>
/// 认证响应数据传输对象
/// </summary>
public record AuthResponseDto
{
    /// <summary>
    /// JWT Token，用于后续API调用的认证
    /// </summary>
    public string Token { get; set; } = "";
    
    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = "";
    
    /// <summary>
    /// 邮箱地址
    /// </summary>
    public string Email { get; set; } = "";
}
