using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BlazorIdle.Server.Auth.DTOs;
using BlazorIdle.Server.Auth.Services;
using System.Security.Claims;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 认证API控制器
/// 提供用户登录、注册、令牌刷新和用户信息查询等功能
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserStore _userStore;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// 构造函数 - 注入认证服务、用户存储和日志记录器
    /// </summary>
    /// <param name="authService">认证服务，负责JWT令牌生成和验证</param>
    /// <param name="userStore">用户存储，负责用户数据管理</param>
    /// <param name="logger">日志记录器</param>
    public AuthController(
        IAuthService authService,
        IUserStore userStore,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _userStore = userStore;
        _logger = logger;
    }

    /// <summary>
    /// 用户登录
    /// 验证用户名和密码，成功后返回JWT令牌和用户信息
    /// </summary>
    /// <param name="request">登录请求，包含用户名和密码</param>
    /// <returns>认证结果，包含JWT令牌、刷新令牌和用户信息</returns>
    /// <response code="200">登录成功，返回令牌和用户信息</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="401">用户名或密码错误</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResult>> Login([FromBody] LoginRequest request)
    {
        try
        {
            // 验证模型状态
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("登录请求参数无效：{ModelState}", ModelState);
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "请求参数无效"
                });
            }

            // 调用认证服务执行登录逻辑
            var result = await _authService.LoginAsync(request.Username, request.Password);

            // 根据认证结果返回相应的HTTP状态码
            if (!result.Success)
            {
                _logger.LogWarning("用户登录失败：{Username}，原因：{Message}", 
                    request.Username, result.Message);
                return Unauthorized(result);
            }

            _logger.LogInformation("用户登录成功：{Username}", request.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录过程中发生错误：{Username}", request.Username);
            return StatusCode(500, new AuthResult
            {
                Success = false,
                Message = "服务器内部错误"
            });
        }
    }

    /// <summary>
    /// 用户注册
    /// 创建新用户账户，成功后自动登录并返回JWT令牌
    /// </summary>
    /// <param name="request">注册请求，包含用户名和密码</param>
    /// <returns>认证结果，包含JWT令牌、刷新令牌和用户信息</returns>
    /// <response code="200">注册成功，返回令牌和用户信息</response>
    /// <response code="400">请求参数无效或用户名已存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResult>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // 验证模型状态
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("注册请求参数无效：{ModelState}", ModelState);
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "请求参数无效"
                });
            }

            // 调用认证服务执行注册逻辑
            var result = await _authService.RegisterAsync(request.Username, request.Password);

            // 根据注册结果返回相应的HTTP状态码
            if (!result.Success)
            {
                _logger.LogWarning("用户注册失败：{Username}，原因：{Message}", 
                    request.Username, result.Message);
                return BadRequest(result);
            }

            _logger.LogInformation("用户注册成功：{Username}", request.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注册过程中发生错误：{Username}", request.Username);
            return StatusCode(500, new AuthResult
            {
                Success = false,
                Message = "服务器内部错误"
            });
        }
    }

    /// <summary>
    /// 刷新JWT令牌
    /// 使用刷新令牌获取新的JWT访问令牌，延长登录会话
    /// </summary>
    /// <param name="request">刷新令牌请求</param>
    /// <returns>新的认证结果，包含新的JWT令牌和刷新令牌</returns>
    /// <response code="200">刷新成功，返回新令牌</response>
    /// <response code="400">请求参数无效</response>
    /// <response code="401">刷新令牌无效或已过期</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResult>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            // 验证模型状态
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("刷新令牌请求参数无效：{ModelState}", ModelState);
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "请求参数无效"
                });
            }

            // 调用认证服务执行令牌刷新逻辑
            var result = await _authService.RefreshTokenAsync(request.RefreshToken);

            // 根据刷新结果返回相应的HTTP状态码
            if (!result.Success)
            {
                _logger.LogWarning("刷新令牌失败：{Message}", result.Message);
                return Unauthorized(result);
            }

            _logger.LogInformation("令牌刷新成功");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新令牌过程中发生错误");
            return StatusCode(500, new AuthResult
            {
                Success = false,
                Message = "服务器内部错误"
            });
        }
    }

    /// <summary>
    /// 获取当前登录用户信息
    /// 需要有效的JWT令牌（通过Authorization头传递）
    /// </summary>
    /// <returns>当前用户信息</returns>
    /// <response code="200">成功获取用户信息</response>
    /// <response code="401">未授权访问（缺少或无效的JWT令牌）</response>
    /// <response code="404">用户不存在</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("me")]
    [Authorize] // 需要JWT认证
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserInfo>> GetCurrentUser()
    {
        try
        {
            // 从JWT Claims中提取用户ID
            // ClaimTypes.NameIdentifier 是在生成JWT时设置的用户唯一标识
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("无法从JWT令牌中提取用户ID");
                return Unauthorized(new { message = "未授权访问" });
            }

            // 从用户存储中获取用户信息
            var user = await _userStore.GetUserByIdAsync(userId);

            if (user == null)
            {
                _logger.LogWarning("用户不存在：{UserId}", userId);
                return NotFound(new { message = "用户不存在" });
            }

            // 返回用户信息（不包含敏感数据如密码哈希）
            return Ok(new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取当前用户信息失败");
            return StatusCode(500, new { message = "服务器内部错误" });
        }
    }

    /// <summary>
    /// 获取所有用户列表（仅供开发和测试使用）
    /// 需要有效的JWT令牌
    /// 注意：生产环境应该移除此端点或添加管理员权限验证
    /// </summary>
    /// <returns>所有用户信息列表</returns>
    /// <response code="200">成功获取用户列表</response>
    /// <response code="401">未授权访问</response>
    /// <response code="500">服务器内部错误</response>
    [HttpGet("users")]
    [Authorize] // 需要JWT认证
    [ProducesResponseType(typeof(IEnumerable<UserInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<UserInfo>>> GetAllUsers()
    {
        try
        {
            // 获取所有用户
            var users = await _userStore.GetAllUsersAsync();
            
            // 转换为UserInfo对象（移除敏感信息）
            var userInfos = users.Select(u => new UserInfo
            {
                Id = u.Id,
                Username = u.Username,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            });

            _logger.LogInformation("获取所有用户列表，共 {Count} 个用户", userInfos.Count());
            return Ok(userInfos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有用户失败");
            return StatusCode(500, new { message = "服务器内部错误" });
        }
    }
}
