using Microsoft.JSInterop;
using System.Net.Http.Json;
using System;

namespace BlazorIdle.Client.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private string? _token;
    private string? _userId;
    private string? _username;

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
    public string? Username => _username;
    public string? UserId => _userId;

    // 新增：认证状态变更事件
    public event Action? AuthStateChanged;
    private void NotifyAuthStateChanged() => AuthStateChanged?.Invoke();

    /// <summary>
    /// 初始化时从 localStorage 加载 token
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _token = await _js.InvokeAsync<string?>("localStorage.getItem", "jwt_token");
            _userId = await _js.InvokeAsync<string?>("localStorage.getItem", "user_id");
            _username = await _js.InvokeAsync<string?>("localStorage.getItem", "username");
        }
        catch
        {
            // localStorage 可能不可用
            _token = null;
            _userId = null;
            _username = null;
        }

        // 通知 UI 刷新
        NotifyAuthStateChanged();
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    public async Task<AuthResult> RegisterAsync(string username, string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/register", new
            {
                username,
                email,
                password
            });

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (data != null)
                {
                    await SaveTokenAsync(data.Token, data.UserId.ToString(), data.Username);
                    return AuthResult.Success();
                }
            }

            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return AuthResult.Failure(error?.Message ?? "注册失败");
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"注册错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    public async Task<AuthResult> LoginAsync(string usernameOrEmail, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/login", new
            {
                usernameOrEmail,
                password
            });

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (data != null)
                {
                    await SaveTokenAsync(data.Token, data.UserId.ToString(), data.Username);
                    return AuthResult.Success();
                }
            }

            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return AuthResult.Failure(error?.Message ?? "登录失败");
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"登录错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 用户登出
    /// </summary>
    public async Task LogoutAsync()
    {
        _token = null;
        _userId = null;
        _username = null;

        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "jwt_token");
            await _js.InvokeVoidAsync("localStorage.removeItem", "user_id");
            await _js.InvokeVoidAsync("localStorage.removeItem", "username");
        }
        catch
        {
            // localStorage 可能不可用
        }

        // 通知 UI 刷新
        NotifyAuthStateChanged();
    }

    /// <summary>
    /// 获取当前 token
    /// </summary>
    public string? GetToken() => _token;

    private async Task SaveTokenAsync(string token, string userId, string username)
    {
        _token = token;
        _userId = userId;
        _username = username;

        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", "jwt_token", token);
            await _js.InvokeVoidAsync("localStorage.setItem", "user_id", userId);
            await _js.InvokeVoidAsync("localStorage.setItem", "username", username);
        }
        catch
        {
            // localStorage 可能不可用
        }

        // 通知 UI 刷新
        NotifyAuthStateChanged();
    }

    // DTOs
    private record AuthResponse(string Token, Guid UserId, string Username, string Email);
    private record ErrorResponse(string Message);
}

public class AuthResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }

    private AuthResult(bool isSuccess, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static AuthResult Success() => new(true);
    public static AuthResult Failure(string errorMessage) => new(false, errorMessage);
}
