using System.Net.Http.Json;
using Blazored.LocalStorage;
using BlazorIdle.Models.Auth;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Services.Auth;

/// <summary>
/// 客户端认证服务实现
/// 负责与服务端API通信进行用户认证，并管理本地存储的Token
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<AuthenticationService> _logger;

    // LocalStorage中的键名（从配置文件读取更好，但这里使用常量以保持简单）
    private const string TOKEN_KEY = "authToken";
    private const string REFRESH_TOKEN_KEY = "refreshToken";
    private const string USER_KEY = "currentUser";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpClient">HTTP客户端（用于调用API）</param>
    /// <param name="localStorage">本地存储服务（用于保存Token）</param>
    /// <param name="logger">日志服务</param>
    public AuthenticationService(
        HttpClient httpClient,
        ILocalStorageService localStorage,
        ILogger<AuthenticationService> logger)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _logger = logger;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        try
        {
            // 创建登录请求
            var request = new LoginRequest { Username = username, Password = password };
            
            // 调用服务端登录API
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                // 解析响应
                var authResult = await response.Content.ReadFromJsonAsync<AuthResult>();

                if (authResult?.Success == true && authResult.Token != null)
                {
                    // 保存Token和用户信息到LocalStorage
                    await _localStorage.SetItemAsync(TOKEN_KEY, authResult.Token);
                    await _localStorage.SetItemAsync(REFRESH_TOKEN_KEY, authResult.RefreshToken);
                    await _localStorage.SetItemAsync(USER_KEY, authResult.User);

                    _logger.LogInformation("用户登录成功：{Username}", username);
                    return authResult;
                }
            }

            _logger.LogWarning("登录失败：{Username}，状态码：{StatusCode}", username, response.StatusCode);
            return new AuthResult
            {
                Success = false,
                Message = "登录失败，请检查用户名和密码"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录过程中发生错误：{Username}", username);
            return new AuthResult
            {
                Success = false,
                Message = "登录失败，请稍后重试"
            };
        }
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    public async Task<AuthResult> RegisterAsync(string username, string password)
    {
        try
        {
            // 创建注册请求
            var request = new RegisterRequest { Username = username, Password = password };
            
            // 调用服务端注册API
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);

            if (response.IsSuccessStatusCode)
            {
                // 解析响应
                var authResult = await response.Content.ReadFromJsonAsync<AuthResult>();

                if (authResult?.Success == true && authResult.Token != null)
                {
                    // 保存Token和用户信息到LocalStorage
                    await _localStorage.SetItemAsync(TOKEN_KEY, authResult.Token);
                    await _localStorage.SetItemAsync(REFRESH_TOKEN_KEY, authResult.RefreshToken);
                    await _localStorage.SetItemAsync(USER_KEY, authResult.User);

                    _logger.LogInformation("用户注册成功：{Username}", username);
                    return authResult;
                }
            }

            _logger.LogWarning("注册失败：{Username}，状态码：{StatusCode}", username, response.StatusCode);
            return new AuthResult
            {
                Success = false,
                Message = "注册失败，用户名可能已存在"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注册过程中发生错误：{Username}", username);
            return new AuthResult
            {
                Success = false,
                Message = "注册失败，请稍后重试"
            };
        }
    }

    /// <summary>
    /// 用户登出
    /// 清除本地存储的所有认证信息
    /// </summary>
    public async Task LogoutAsync()
    {
        try
        {
            // 从LocalStorage中移除Token和用户信息
            await _localStorage.RemoveItemAsync(TOKEN_KEY);
            await _localStorage.RemoveItemAsync(REFRESH_TOKEN_KEY);
            await _localStorage.RemoveItemAsync(USER_KEY);

            _logger.LogInformation("用户已登出");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登出过程中发生错误");
        }
    }

    /// <summary>
    /// 检查是否已登录
    /// 通过检查LocalStorage中是否存在Token来判断
    /// </summary>
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>(TOKEN_KEY);
            return !string.IsNullOrEmpty(token);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取当前JWT访问令牌
    /// </summary>
    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _localStorage.GetItemAsync<string>(TOKEN_KEY);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取当前登录用户信息
    /// </summary>
    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        try
        {
            return await _localStorage.GetItemAsync<UserInfo>(USER_KEY);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 刷新JWT访问令牌
    /// 使用存储的刷新令牌获取新的访问令牌
    /// </summary>
    public async Task<AuthResult> RefreshTokenAsync()
    {
        try
        {
            // 获取刷新令牌
            var refreshToken = await _localStorage.GetItemAsync<string>(REFRESH_TOKEN_KEY);

            if (string.IsNullOrEmpty(refreshToken))
            {
                return new AuthResult
                {
                    Success = false,
                    Message = "刷新令牌不存在"
                };
            }

            // 创建刷新请求
            var request = new RefreshTokenRequest { RefreshToken = refreshToken };
            
            // 调用服务端刷新API
            var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh", request);

            if (response.IsSuccessStatusCode)
            {
                // 解析响应
                var authResult = await response.Content.ReadFromJsonAsync<AuthResult>();

                if (authResult?.Success == true && authResult.Token != null)
                {
                    // 保存新的Token
                    await _localStorage.SetItemAsync(TOKEN_KEY, authResult.Token);
                    await _localStorage.SetItemAsync(REFRESH_TOKEN_KEY, authResult.RefreshToken);

                    _logger.LogInformation("Token刷新成功");
                    return authResult;
                }
            }

            _logger.LogWarning("Token刷新失败，状态码：{StatusCode}", response.StatusCode);
            return new AuthResult
            {
                Success = false,
                Message = "Token刷新失败，请重新登录"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token刷新过程中发生错误");
            return new AuthResult
            {
                Success = false,
                Message = "Token刷新失败，请稍后重试"
            };
        }
    }
}
