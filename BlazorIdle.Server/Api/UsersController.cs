using BlazorIdle.Server.Application.Auth;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 用户管理 API 控制器
/// </summary>
/// <remarks>
/// 提供的功能：
/// - 查询当前登录用户信息
/// - 查询指定用户信息
/// - 查询用户的所有角色
/// - 更新用户信息（仅限本人）
/// 
/// 用户信息包含：
/// - 基本信息（ID、用户名、邮箱）
/// - 时间戳（创建时间、最后登录时间）
/// - 关联的角色列表
/// 
/// 权限控制：
/// - 查询需要JWT认证
/// - 更新仅允许用户本人操作
/// 
/// 认证要求：
/// - 所有接口需要 JWT 认证
/// 
/// 基础路由：/api/users
/// </remarks>
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
    /// <returns>当前登录用户的完整信息，包含角色列表</returns>
    /// <response code="200">返回用户信息</response>
    /// <response code="401">未登录或Token无效</response>
    /// <response code="404">用户不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 从JWT Token中提取用户ID
    /// - 返回用户基本信息和所有角色
    /// - 角色按RosterOrder排序
    /// - 常用于用户中心、个人资料页面
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/users/me
    /// Authorization: Bearer {JWT_TOKEN}
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "id": "123e4567-e89b-12d3-a456-426614174000",
    ///   "username": "player123",
    ///   "email": "player@example.com",
    ///   "createdAt": "2025-01-01T00:00:00Z",
    ///   "lastLoginAt": "2025-10-15T10:30:00Z",
    ///   "characters": [
    ///     {
    ///       "id": "456e7890-e89b-12d3-a456-426614174000",
    ///       "name": "我的战士",
    ///       "level": 50,
    ///       "profession": "Warrior",
    ///       "rosterOrder": 0
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
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
            Characters = user.Characters
                .OrderBy(c => c.RosterOrder)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Level,
                    c.Profession,
                    c.RosterOrder
                })
        });
    }

    /// <summary>
    /// 获取指定用户信息
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <returns>指定用户的完整信息，包含角色列表</returns>
    /// <response code="200">返回用户信息</response>
    /// <response code="401">未登录</response>
    /// <response code="404">用户不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 根据用户ID查询用户信息
    /// - 返回用户基本信息和所有角色
    /// - 角色按RosterOrder排序
    /// - 可用于查看其他玩家信息、好友列表等
    /// 
    /// 隐私说明：
    /// - 当前版本返回完整信息（包括邮箱）
    /// - 未来可能需要限制敏感信息的可见性
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/users/123e4567-e89b-12d3-a456-426614174000
    /// Authorization: Bearer {JWT_TOKEN}
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "id": "123e4567-e89b-12d3-a456-426614174000",
    ///   "username": "player123",
    ///   "email": "player@example.com",
    ///   "createdAt": "2025-01-01T00:00:00Z",
    ///   "lastLoginAt": "2025-10-15T10:30:00Z",
    ///   "characters": [
    ///     {
    ///       "id": "456e7890-e89b-12d3-a456-426614174000",
    ///       "name": "我的战士",
    ///       "level": 50,
    ///       "profession": "Warrior",
    ///       "rosterOrder": 0
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
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
            Characters = user.Characters
                .OrderBy(c => c.RosterOrder)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Level,
                    c.Profession,
                    c.RosterOrder
                })
        });
    }

    /// <summary>
    /// 获取用户的所有角色
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <returns>用户的角色列表</returns>
    /// <response code="200">返回角色列表（可能为空）</response>
    /// <response code="401">未登录</response>
    /// <remarks>
    /// 功能说明：
    /// - 查询指定用户的所有角色
    /// - 按RosterOrder排序
    /// - 包含角色创建时间
    /// - 即使用户没有角色也返回空数组（而不是404）
    /// 
    /// 与获取用户信息的区别：
    /// - 此接口仅返回角色列表，不包含用户基本信息
    /// - 适用于需要单独获取角色列表的场景
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/users/123e4567-e89b-12d3-a456-426614174000/characters
    /// Authorization: Bearer {JWT_TOKEN}
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// [
    ///   {
    ///     "id": "456e7890-e89b-12d3-a456-426614174000",
    ///     "name": "我的战士",
    ///     "level": 50,
    ///     "profession": "Warrior",
    ///     "rosterOrder": 0,
    ///     "createdAt": "2025-01-15T08:00:00Z"
    ///   },
    ///   {
    ///     "id": "456e7890-e89b-12d3-a456-426614174111",
    ///     "name": "我的法师",
    ///     "level": 35,
    ///     "profession": "Mage",
    ///     "rosterOrder": 1,
    ///     "createdAt": "2025-02-01T10:30:00Z"
    ///   }
    /// ]
    /// </code>
    /// </remarks>
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
    /// <param name="id">用户ID</param>
    /// <param name="dto">更新数据传输对象</param>
    /// <returns>更新结果</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">邮箱已被使用</response>
    /// <response code="401">未登录</response>
    /// <response code="403">无权限（只能更新自己的信息）</response>
    /// <response code="404">用户不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 更新用户的邮箱地址
    /// - 仅允许用户更新自己的信息（通过JWT验证）
    /// - 验证邮箱唯一性（不能与其他用户重复）
    /// - 自动更新UpdatedAt时间戳
    /// 
    /// 权限验证：
    /// - 从JWT Token提取当前用户ID
    /// - 对比路径中的用户ID
    /// - 不一致则返回403 Forbidden
    /// 
    /// 邮箱验证：
    /// - 如果不提供邮箱或邮箱未变更，跳过验证
    /// - 如果新邮箱已被其他用户使用，返回400错误
    /// 
    /// 未来扩展：
    /// - 可添加更多可更新字段（头像、昵称等）
    /// - 可添加邮箱格式验证
    /// - 可添加邮箱验证流程
    /// 
    /// 请求示例：
    /// <code>
    /// PUT /api/users/123e4567-e89b-12d3-a456-426614174000
    /// Authorization: Bearer {JWT_TOKEN}
    /// {
    ///   "email": "newemail@example.com"
    /// }
    /// </code>
    /// 
    /// 响应示例（成功）：
    /// <code>
    /// {
    ///   "message": "用户信息更新成功"
    /// }
    /// </code>
    /// 
    /// 响应示例（失败）：
    /// <code>
    /// {
    ///   "message": "邮箱已被其他用户使用"
    /// }
    /// </code>
    /// </remarks>
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

/// <summary>
/// 更新用户信息数据传输对象
/// </summary>
/// <param name="Email">新邮箱地址（可选）</param>
public record UpdateUserDto(string? Email);
