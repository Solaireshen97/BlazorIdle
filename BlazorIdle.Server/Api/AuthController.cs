using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BlazorIdle.Server.Auth.DTOs;
using BlazorIdle.Server.Auth.Services;
using System.Security.Claims;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 认证API控制器
/// 提供用户登录、注册、令牌刷新等认证相关功能
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserStore _userStore;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// 构造函数 - 注入认证服务、用户存储和日志服务
    /// </summary>
    /// <param name="authService">认证服务，用于处理登录、注册、令牌生成等业务逻辑</param>
    /// <param name="userStore">用户存储，用于访问用户数据</param>
    /// <param name="logger">日志记录器，用于记录操作日志和错误信息</param>
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
    /// <response code="200">登录成功，返回认证结果</response>
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
                _logger.LogWarning("登录请求验证失败：{ModelState}", ModelState);
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "请求参数无效"
                });
            }

            // 调用认证服务执行登录
            var result = await _authService.LoginAsync(request.Username, request.Password);

            // 检查登录结果
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
            // 记录异常并返回通用错误信息（不暴露内部细节）
            _logger.LogError(ex, "登录过程中发生错误：{Username}", request.Username);
            return StatusCode(500, new AuthResult
            {
                Success = false,
                Message = "服务器内部错误，请稍后重试"
            });
        }
    }

    /// <summary>
    /// 用户注册
    /// 创建新用户账户，成功后自动登录并返回JWT令牌
    /// </summary>
    /// <param name="request">注册请求，包含用户名和密码</param>
    /// <returns>认证结果，包含JWT令牌、刷新令牌和用户信息</returns>
    /// <response code="200">注册成功，返回认证结果</response>
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
                _logger.LogWarning("注册请求验证失败：{ModelState}", ModelState);
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "请求参数无效"
                });
            }

            // 调用认证服务执行注册
            var result = await _authService.RegisterAsync(request.Username, request.Password);

            // 检查注册结果
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
            // 记录异常并返回通用错误信息
            _logger.LogError(ex, "注册过程中发生错误：{Username}", request.Username);
            return StatusCode(500, new AuthResult
            {
                Success = false,
                Message = "服务器内部错误，请稍后重试"
            });
        }
    }

    /// <summary>
    /// 刷新JWT令牌
    /// 使用刷新令牌获取新的访问令牌，延长用户会话
    /// </summary>
    /// <param name="request">刷新令牌请求，包含刷新令牌</param>
    /// <returns>新的认证结果，包含新的JWT令牌和刷新令牌</returns>
    /// <response code="200">刷新成功，返回新的认证结果</response>
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
                _logger.LogWarning("刷新令牌请求验证失败：{ModelState}", ModelState);
                return BadRequest(new AuthResult
                {
                    Success = false,
                    Message = "请求参数无效"
                });
            }

            // 调用认证服务刷新令牌
            var result = await _authService.RefreshTokenAsync(request.RefreshToken);

            // 检查刷新结果
            if (!result.Success)
            {
                _logger.LogWarning("令牌刷新失败：{Message}", result.Message);
                return Unauthorized(result);
            }

            _logger.LogInformation("令牌刷新成功");
            return Ok(result);
        }
        catch (Exception ex)
        {
            // 记录异常并返回通用错误信息
            _logger.LogError(ex, "刷新令牌过程中发生错误");
            return StatusCode(500, new AuthResult
            {
                Success = false,
                Message = "服务器内部错误，请稍后重试"
            });
        }
    }

    /// <summary>
    /// 获取当前登录用户信息
    /// 需要JWT令牌认证，从令牌中提取用户ID并返回用户信息
    /// </summary>
    /// <returns>用户信息</returns>
    /// <response code="200">成功返回用户信息</response>
    /// <response code="401">未授权访问或令牌无效</response>
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
            // 从JWT令牌的Claims中提取用户ID
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("未找到用户ID，请求未授权");
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
            // 记录异常并返回通用错误信息
            _logger.LogError(ex, "获取当前用户信息失败");
            return StatusCode(500, new { message = "服务器内部错误" });
        }
    }

    /// <summary>
    /// 获取所有用户列表（仅供测试和开发使用）
    /// 需要JWT令牌认证
    /// </summary>
    /// <returns>所有用户的信息列表</returns>
    /// <response code="200">成功返回用户列表</response>
    /// <response code="401">未授权访问</response>
    /// <response code="500">服务器内部错误</response>
    /// <remarks>
    /// 警告：此端点仅用于开发和测试，生产环境应移除或添加权限控制
    /// </remarks>
    [HttpGet("users")]
    [Authorize] // 需要JWT认证
    [ProducesResponseType(typeof(IEnumerable<UserInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<UserInfo>>> GetAllUsers()
    {
        try
        {
            // 从用户存储中获取所有用户
            var users = await _userStore.GetAllUsersAsync();
            
            // 将用户实体转换为用户信息DTO（不包含敏感数据）
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
            // 记录异常并返回通用错误信息
            _logger.LogError(ex, "获取所有用户失败");
            return StatusCode(500, new { message = "服务器内部错误" });
        }
    }
}
