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

/// <summary>
/// 角色管理 API 控制器
/// </summary>
/// <remarks>
/// 提供的功能：
/// - 创建新角色（需要认证，每个用户最多5个角色）
/// - 查询角色信息
/// - 绑定角色到用户账号
/// - 调整角色在 Roster 中的显示顺序
/// - 角色心跳更新（用于离线检测和自动结算）
/// 
/// 认证要求：
/// - Create, BindToUser, ReorderCharacter 需要 JWT 认证
/// - Get, Heartbeat 无需认证
/// 
/// 基础路由：/api/characters
/// </remarks>
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

    /// <summary>
    /// 创建新角色
    /// </summary>
    /// <param name="dto">角色创建数据传输对象</param>
    /// <returns>包含角色ID、名称和顺序的对象</returns>
    /// <response code="200">角色创建成功</response>
    /// <response code="400">角色数量达到上限（最多5个）</response>
    /// <response code="401">未登录或认证失败</response>
    /// <remarks>
    /// 功能说明：
    /// - 需要用户登录（JWT认证）
    /// - 每个用户最多创建5个角色
    /// - 如果DTO中未提供属性值，使用职业默认属性
    /// - 新角色等级为1，RosterOrder根据现有角色数量自动设置
    /// 
    /// 职业默认属性：
    /// - 战士(Warrior): 力量20, 敏捷10, 智力5, 耐力15
    /// - 游侠(Ranger): 力量10, 敏捷20, 智力8, 耐力12
    /// - 其他: 力量10, 敏捷10, 智力10, 耐力10
    /// 
    /// 请求示例：
    /// <code>
    /// POST /api/characters
    /// {
    ///   "name": "我的战士",
    ///   "profession": "Warrior",
    ///   "strength": 25
    /// }
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "id": "123e4567-e89b-12d3-a456-426614174000",
    ///   "name": "我的战士",
    ///   "rosterOrder": 0
    /// }
    /// </code>
    /// </remarks>
    [HttpPost]
    [Authorize]
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
        await Infrastructure.Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(_db);
        return Ok(new { c.Id, c.Name, c.RosterOrder });
    }

    /// <summary>
    /// 获取角色信息
    /// </summary>
    /// <param name="id">角色ID</param>
    /// <returns>角色详细信息，包含ID、名称、等级、职业和属性</returns>
    /// <response code="200">成功获取角色信息</response>
    /// <response code="404">角色不存在</response>
    /// <remarks>
    /// 无需认证，任何人都可以查询角色信息
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/characters/123e4567-e89b-12d3-a456-426614174000
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "id": "123e4567-e89b-12d3-a456-426614174000",
    ///   "name": "我的战士",
    ///   "level": 10,
    ///   "profession": "Warrior",
    ///   "strength": 25,
    ///   "agility": 12,
    ///   "intellect": 8,
    ///   "stamina": 20
    /// }
    /// </code>
    /// </remarks>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id)
    {
        var c = await _db.Characters.FirstOrDefaultAsync(x => x.Id == id);
        return c is null
            ? NotFound()
            : Ok(new { c.Id, c.Name, c.Level, c.Profession, c.Strength, c.Agility, c.Intellect, c.Stamina });
    }

    /// <summary>
    /// 将未绑定的角色绑定到当前用户
    /// </summary>
    /// <param name="id">角色ID</param>
    /// <returns>绑定结果</returns>
    /// <response code="200">绑定成功</response>
    /// <response code="400">角色已绑定到其他用户</response>
    /// <response code="401">未登录或认证失败</response>
    /// <response code="404">角色不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 需要用户登录（JWT认证）
    /// - 只能绑定未归属任何用户的角色
    /// - 绑定后自动设置 RosterOrder 为用户当前角色数量
    /// 
    /// 使用场景：
    /// - 将测试角色绑定到正式账号
    /// - 角色转移或认领
    /// 
    /// 请求示例：
    /// <code>
    /// PUT /api/characters/123e4567-e89b-12d3-a456-426614174000/bind-user
    /// </code>
    /// </remarks>
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

        await Infrastructure.Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(_db);
        return Ok(new { message = "角色绑定成功" });
    }

    /// <summary>
    /// 调整角色在 Roster 中的显示顺序
    /// </summary>
    /// <param name="id">角色ID</param>
    /// <param name="dto">包含新顺序值的数据传输对象</param>
    /// <returns>调整结果</returns>
    /// <response code="200">顺序调整成功</response>
    /// <response code="401">未登录或认证失败</response>
    /// <response code="403">无权操作该角色（角色不属于当前用户）</response>
    /// <response code="404">角色不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 需要用户登录（JWT认证）
    /// - 只能调整属于自己的角色顺序
    /// - RosterOrder 用于前端显示角色列表的排序
    /// 
    /// 请求示例：
    /// <code>
    /// PUT /api/characters/123e4567-e89b-12d3-a456-426614174000/reorder
    /// {
    ///   "rosterOrder": 2
    /// }
    /// </code>
    /// </remarks>
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
        await Infrastructure.Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(_db);
        return Ok(new { message = "角色顺序调整成功" });
    }

    /// <summary>
    /// 更新角色心跳时间，标记角色在线状态
    /// </summary>
    /// <param name="id">角色ID</param>
    /// <returns>心跳更新结果，可能包含离线结算信息</returns>
    /// <response code="200">心跳更新成功</response>
    /// <response code="404">角色不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 更新角色的 LastSeenAtUtc 时间戳
    /// - 自动检测离线状态（基于配置的离线阈值，默认60秒）
    /// - 如果检测到离线，自动触发离线结算（如果启用）
    /// - 无需认证，客户端定期调用
    /// 
    /// 配置参数（appsettings.json）：
    /// - Offline:OfflineDetectionSeconds - 离线检测阈值（默认60秒）
    /// - Offline:AutoApplyRewards - 是否自动应用离线收益（默认true）
    /// 
    /// 处理流程：
    /// 1. 查询角色信息
    /// 2. 计算自上次心跳以来的时间
    /// 3. 如果超过离线阈值，触发离线结算
    /// 4. 如果启用自动应用，立即应用离线收益
    /// 5. 更新心跳时间戳
    /// 
    /// 请求示例：
    /// <code>
    /// POST /api/characters/123e4567-e89b-12d3-a456-426614174000/heartbeat
    /// </code>
    /// 
    /// 响应示例（无离线）：
    /// <code>
    /// {
    ///   "message": "心跳更新成功",
    ///   "timestamp": "2025-10-15T10:30:00Z",
    ///   "offlineSettlement": null
    /// }
    /// </code>
    /// 
    /// 响应示例（有离线结算）：
    /// <code>
    /// {
    ///   "message": "心跳更新成功",
    ///   "timestamp": "2025-10-15T10:30:00Z",
    ///   "offlineSettlement": {
    ///     "wasOffline": true,
    ///     "offlineDuration": 3600,
    ///     "settlement": { ... }
    ///   }
    /// }
    /// </code>
    /// 
    /// 注意事项：
    /// - 建议客户端每30秒调用一次
    /// - 离线结算失败不会阻止心跳更新
    /// - 结算错误会记录日志但不返回给客户端
    /// </remarks>
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
        
        // 更新心跳时间
        // 优化：使用内存缓冲，仅标记为dirty，由PersistenceCoordinator定期批量保存
        // CheckAndSettleAsync 会更新 LastSeenAtUtc，所以这里需要重新获取
        character = await _db.Characters.FirstOrDefaultAsync(c => c.Id == id);
        if (character != null)
        {
            character.LastSeenAtUtc = DateTime.UtcNow;
            
            // 检查是否启用内存缓冲
            var enableMemoryBuffering = _configuration.GetValue<bool>("Persistence:EnableMemoryBuffering", false);
            if (enableMemoryBuffering)
            {
                // 使用内存缓冲：只更新内存中的实体，标记为dirty
                // PersistenceCoordinator 会根据配置的SaveIntervalMs定期批量保存
                var characterManager = HttpContext.RequestServices
                    .GetService<Infrastructure.DatabaseOptimization.Abstractions.IMemoryStateManager<Domain.Characters.Character>>();
                    
                if (characterManager != null)
                {
                    characterManager.Update(character);
                    // 不再调用 SaveChangesAsync，让后台服务批量保存
                }
                else
                {
                    // Fallback: 如果无法获取MemoryStateManager，直接保存
                    await Infrastructure.Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(_db);
                }
            }
            else
            {
                // 未启用内存缓冲：保持原有的立即保存行为
                await Infrastructure.Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(_db);
            }
        }
        
        return Ok(new
        {
            message = "心跳更新成功",
            timestamp = character?.LastSeenAtUtc,
            offlineSettlement = offlineResult
        });
    }

    /// <summary>
    /// 根据职业获取默认属性值
    /// </summary>
    /// <param name="p">职业类型</param>
    /// <returns>元组包含力量、敏捷、智力、耐力的默认值</returns>
    /// <remarks>
    /// 职业默认属性配置：
    /// - 战士(Warrior): 力量20, 敏捷10, 智力5, 耐力15 - 偏向近战物理输出
    /// - 游侠(Ranger): 力量10, 敏捷20, 智力8, 耐力12 - 偏向远程物理输出
    /// - 其他职业: 平衡属性 10, 10, 10, 10
    /// </remarks>
    private static (int str, int agi, int intel, int sta) DefaultAttributesFor(Profession p)
        => p switch
        {
            Profession.Warrior => (20, 10, 5, 15),
            Profession.Ranger => (10, 20, 8, 12),
            _ => (10, 10, 10, 10)
        };
}

/// <summary>
/// 角色创建数据传输对象
/// </summary>
/// <param name="Name">角色名称（必需）</param>
/// <param name="Profession">职业类型（必需）</param>
/// <param name="Strength">力量属性（可选，未提供则使用职业默认值）</param>
/// <param name="Agility">敏捷属性（可选，未提供则使用职业默认值）</param>
/// <param name="Intellect">智力属性（可选，未提供则使用职业默认值）</param>
/// <param name="Stamina">耐力属性（可选，未提供则使用职业默认值）</param>
public record CreateCharacterDto(
    string Name,
    Profession Profession,
    int? Strength = null,
    int? Agility = null,
    int? Intellect = null,
    int? Stamina = null
);

/// <summary>
/// 角色顺序调整数据传输对象
/// </summary>
/// <param name="RosterOrder">新的显示顺序值</param>
public record ReorderDto(int RosterOrder);