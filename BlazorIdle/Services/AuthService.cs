using Microsoft.JSInterop;
using System.Net.Http.Json;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Client.Services;

/// <summary>
/// 客户端认证服务
/// 提供用户注册、登录、登出等认证功能，并管理JWT Token的存储和状态
/// </summary>
/// <remarks>
/// 该服务负责：
/// 1. 与服务端认证API交互（注册、登录等）
/// 2. 管理JWT Token的本地存储（使用LocalStorage）
/// 3. 维护用户的认证状态
/// 4. 提供认证状态变更事件，供其他组件订阅
/// </remarks>
public class AuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    
    private string? _token;
    private string? _userId;
    private string? _username;

    /// <summary>
    /// 配置键名常量 - 认证API基础URL
    /// </summary>
    private const string ConfigKeyApiBaseUrl = "Auth:ApiBaseUrl";

    /// <summary>
    /// 配置键名常量 - 注册端点
    /// </summary>
    private const string ConfigKeyRegisterEndpoint = "Auth:RegisterEndpoint";

    /// <summary>
    /// 配置键名常量 - 登录端点
    /// </summary>
    private const string ConfigKeyLoginEndpoint = "Auth:LoginEndpoint";

    /// <summary>
    /// 配置键名常量 - Token存储键
    /// </summary>
    private const string ConfigKeyTokenStorageKey = "Auth:TokenStorageKey";

    /// <summary>
    /// 配置键名常量 - 用户ID存储键
    /// </summary>
    private const string ConfigKeyUserIdStorageKey = "Auth:UserIdStorageKey";

    /// <summary>
    /// 配置键名常量 - 用户名存储键
    /// </summary>
    private const string ConfigKeyUsernameStorageKey = "Auth:UsernameStorageKey";

    /// <summary>
    /// 默认认证API基础URL
    /// </summary>
    private const string DefaultApiBaseUrl = "/api/auth";

    /// <summary>
    /// 默认注册端点
    /// </summary>
    private const string DefaultRegisterEndpoint = "/register";

    /// <summary>
    /// 默认登录端点
    /// </summary>
    private const string DefaultLoginEndpoint = "/login";

    /// <summary>
    /// 默认Token存储键
    /// </summary>
    private const string DefaultTokenStorageKey = "jwt_token";

    /// <summary>
    /// 默认用户ID存储键
    /// </summary>
    private const string DefaultUserIdStorageKey = "user_id";

    /// <summary>
    /// 默认用户名存储键
    /// </summary>
    private const string DefaultUsernameStorageKey = "username";

    /// <summary>
    /// 初始化认证服务
    /// </summary>
    /// <param name="http">HTTP客户端，用于与服务端API通信</param>
    /// <param name="js">JavaScript互操作运行时，用于访问浏览器LocalStorage</param>
    /// <param name="configuration">应用程序配置，用于读取认证相关配置</param>
    /// <param name="logger">日志记录器，用于记录认证操作和错误</param>
    public AuthService(
        HttpClient http, 
        IJSRuntime js, 
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _js = js ?? throw new ArgumentNullException(nameof(js));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取用户是否已认证
    /// </summary>
    /// <value>如果用户已登录（拥有有效Token）则返回true，否则返回false</value>
    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);

    /// <summary>
    /// 获取当前登录用户的用户名
    /// </summary>
    /// <value>用户名字符串，如果未登录则返回null</value>
    public string? Username => _username;

    /// <summary>
    /// 获取当前登录用户的ID
    /// </summary>
    /// <value>用户ID字符串，如果未登录则返回null</value>
    public string? UserId => _userId;

    /// <summary>
    /// 认证状态变更事件
    /// </summary>
    /// <remarks>
    /// 当用户登录或登出时触发，供UI组件订阅以更新显示状态
    /// </remarks>
    public event Action? AuthStateChanged;

    /// <summary>
    /// 用户认证成功事件
    /// </summary>
    /// <remarks>
    /// 在用户成功登录或注册后触发，可用于建立SignalR连接等后续操作
    /// </remarks>
    public event Func<Task>? OnAuthenticated;

    /// <summary>
    /// 用户取消认证事件
    /// </summary>
    /// <remarks>
    /// 在用户登出后触发，可用于断开SignalR连接等清理操作
    /// </remarks>
    public event Func<Task>? OnUnauthenticated;

    /// <summary>
    /// 通知所有订阅者认证状态已变更
    /// </summary>
    private void NotifyAuthStateChanged()
    {
        try
        {
            AuthStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发认证状态变更事件时发生错误");
        }
    }

    /// <summary>
    /// 初始化认证服务，从浏览器LocalStorage加载已保存的Token和用户信息
    /// </summary>
    /// <remarks>
    /// 应在应用程序启动时调用此方法，以恢复用户的登录状态。
    /// 如果LocalStorage不可用（如隐私模式），将静默失败而不抛出异常。
    /// </remarks>
    public async Task InitializeAsync()
    {
        try
        {
            // 从配置读取存储键名
            var tokenKey = _configuration[ConfigKeyTokenStorageKey] ?? DefaultTokenStorageKey;
            var userIdKey = _configuration[ConfigKeyUserIdStorageKey] ?? DefaultUserIdStorageKey;
            var usernameKey = _configuration[ConfigKeyUsernameStorageKey] ?? DefaultUsernameStorageKey;

            // 从LocalStorage读取保存的认证信息
            _token = await _js.InvokeAsync<string?>("localStorage.getItem", tokenKey);
            _userId = await _js.InvokeAsync<string?>("localStorage.getItem", userIdKey);
            _username = await _js.InvokeAsync<string?>("localStorage.getItem", usernameKey);

            if (IsAuthenticated)
            {
                _logger.LogInformation("成功从LocalStorage恢复用户认证状态: {Username}", _username);
            }
            else
            {
                _logger.LogDebug("LocalStorage中未找到有效的认证信息");
            }
        }
        catch (Exception ex)
        {
            // LocalStorage可能不可用（如浏览器隐私模式、权限限制等）
            _logger.LogWarning(ex, "从LocalStorage加载认证信息失败，可能是隐私模式或权限限制");
            
            _token = null;
            _userId = null;
            _username = null;
        }

        // 通知UI组件更新认证状态显示
        NotifyAuthStateChanged();
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    /// <param name="username">用户名（唯一）</param>
    /// <param name="email">电子邮箱（唯一）</param>
    /// <param name="password">密码（明文，将在服务端加密存储）</param>
    /// <returns>
    /// 注册结果对象，包含成功状态和错误消息（如果有）
    /// 注册成功后将自动保存Token并触发认证事件
    /// </returns>
    /// <remarks>
    /// 注册成功后用户将自动登录，无需再次调用LoginAsync
    /// </remarks>
    public async Task<AuthResult> RegisterAsync(string username, string email, string password)
    {
        // 参数验证
        if (string.IsNullOrWhiteSpace(username))
        {
            return AuthResult.Failure("用户名不能为空");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return AuthResult.Failure("电子邮箱不能为空");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return AuthResult.Failure("密码不能为空");
        }

        try
        {
            // 从配置读取API端点
            var apiBaseUrl = _configuration[ConfigKeyApiBaseUrl] ?? DefaultApiBaseUrl;
            var registerEndpoint = _configuration[ConfigKeyRegisterEndpoint] ?? DefaultRegisterEndpoint;
            var fullUrl = $"{apiBaseUrl}{registerEndpoint}";

            _logger.LogInformation("尝试注册新用户: {Username}", username);

            // 发送注册请求到服务端
            var response = await _http.PostAsJsonAsync(fullUrl, new
            {
                username,
                email,
                password
            });

            if (response.IsSuccessStatusCode)
            {
                // 注册成功，解析响应数据
                var data = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (data != null)
                {
                    // 保存Token和用户信息
                    await SaveTokenAsync(data.Token, data.UserId.ToString(), data.Username);
                    
                    _logger.LogInformation("用户 {Username} 注册成功", username);
                    return AuthResult.Success();
                }

                _logger.LogError("注册请求成功但响应数据为空");
                return AuthResult.Failure("注册失败：服务器响应数据无效");
            }

            // 注册失败，尝试解析错误消息
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            var errorMessage = error?.Message ?? $"注册失败：HTTP {response.StatusCode}";
            
            _logger.LogWarning("用户 {Username} 注册失败: {Error}", username, errorMessage);
            return AuthResult.Failure(errorMessage);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "注册请求发送失败，可能是网络问题");
            return AuthResult.Failure("注册失败：无法连接到服务器，请检查网络连接");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注册过程中发生未预期的错误");
            return AuthResult.Failure($"注册错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="usernameOrEmail">用户名或电子邮箱</param>
    /// <param name="password">密码（明文）</param>
    /// <returns>
    /// 登录结果对象，包含成功状态和错误消息（如果有）
    /// 登录成功后将自动保存Token并触发认证事件
    /// </returns>
    /// <remarks>
    /// 支持使用用户名或电子邮箱进行登录，服务端会自动识别
    /// </remarks>
    public async Task<AuthResult> LoginAsync(string usernameOrEmail, string password)
    {
        // 参数验证
        if (string.IsNullOrWhiteSpace(usernameOrEmail))
        {
            return AuthResult.Failure("用户名或邮箱不能为空");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return AuthResult.Failure("密码不能为空");
        }

        try
        {
            // 从配置读取API端点
            var apiBaseUrl = _configuration[ConfigKeyApiBaseUrl] ?? DefaultApiBaseUrl;
            var loginEndpoint = _configuration[ConfigKeyLoginEndpoint] ?? DefaultLoginEndpoint;
            var fullUrl = $"{apiBaseUrl}{loginEndpoint}";

            _logger.LogInformation("尝试登录: {UsernameOrEmail}", usernameOrEmail);

            // 发送登录请求到服务端
            var response = await _http.PostAsJsonAsync(fullUrl, new
            {
                usernameOrEmail,
                password
            });

            if (response.IsSuccessStatusCode)
            {
                // 登录成功，解析响应数据
                var data = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (data != null)
                {
                    // 保存Token和用户信息
                    await SaveTokenAsync(data.Token, data.UserId.ToString(), data.Username);
                    
                    _logger.LogInformation("用户 {Username} 登录成功", data.Username);
                    return AuthResult.Success();
                }

                _logger.LogError("登录请求成功但响应数据为空");
                return AuthResult.Failure("登录失败：服务器响应数据无效");
            }

            // 登录失败，尝试解析错误消息
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            var errorMessage = error?.Message ?? $"登录失败：HTTP {response.StatusCode}";
            
            _logger.LogWarning("登录失败: {Error}", errorMessage);
            return AuthResult.Failure(errorMessage);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "登录请求发送失败，可能是网络问题");
            return AuthResult.Failure("登录失败：无法连接到服务器，请检查网络连接");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录过程中发生未预期的错误");
            return AuthResult.Failure($"登录错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 用户登出
    /// </summary>
    /// <remarks>
    /// 清除内存和LocalStorage中保存的认证信息，
    /// 并触发登出事件通知其他组件（如断开SignalR连接）
    /// </remarks>
    public async Task LogoutAsync()
    {
        var currentUsername = _username;

        // 清除内存中的认证信息
        _token = null;
        _userId = null;
        _username = null;

        try
        {
            // 从配置读取存储键名
            var tokenKey = _configuration[ConfigKeyTokenStorageKey] ?? DefaultTokenStorageKey;
            var userIdKey = _configuration[ConfigKeyUserIdStorageKey] ?? DefaultUserIdStorageKey;
            var usernameKey = _configuration[ConfigKeyUsernameStorageKey] ?? DefaultUsernameStorageKey;

            // 从LocalStorage中删除认证信息
            await _js.InvokeVoidAsync("localStorage.removeItem", tokenKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", userIdKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", usernameKey);

            _logger.LogInformation("用户 {Username} 成功登出", currentUsername);
        }
        catch (Exception ex)
        {
            // LocalStorage可能不可用，但不影响登出流程
            _logger.LogWarning(ex, "从LocalStorage删除认证信息时发生错误，但登出仍然成功");
        }

        // 触发登出事件，通知订阅者执行清理操作（如断开SignalR连接）
        if (OnUnauthenticated != null)
        {
            try
            {
                await OnUnauthenticated.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行登出事件处理程序时发生错误");
            }
        }

        // 通知UI组件更新认证状态显示
        NotifyAuthStateChanged();
    }

    /// <summary>
    /// 获取当前用户的JWT Token
    /// </summary>
    /// <returns>JWT Token字符串，如果未登录则返回null</returns>
    /// <remarks>
    /// 此方法可用于需要手动添加Authorization头的HTTP请求
    /// </remarks>
    public string? GetToken() => _token;

    /// <summary>
    /// 保存Token和用户信息到内存和LocalStorage
    /// </summary>
    /// <param name="token">JWT Token字符串</param>
    /// <param name="userId">用户ID</param>
    /// <param name="username">用户名</param>
    /// <remarks>
    /// 此方法在登录或注册成功后调用，
    /// 同时触发登录事件通知其他组件（如建立SignalR连接）
    /// </remarks>
    private async Task SaveTokenAsync(string token, string userId, string username)
    {
        // 保存到内存
        _token = token;
        _userId = userId;
        _username = username;

        try
        {
            // 从配置读取存储键名
            var tokenKey = _configuration[ConfigKeyTokenStorageKey] ?? DefaultTokenStorageKey;
            var userIdKey = _configuration[ConfigKeyUserIdStorageKey] ?? DefaultUserIdStorageKey;
            var usernameKey = _configuration[ConfigKeyUsernameStorageKey] ?? DefaultUsernameStorageKey;

            // 保存到LocalStorage，实现持久化存储
            await _js.InvokeVoidAsync("localStorage.setItem", tokenKey, token);
            await _js.InvokeVoidAsync("localStorage.setItem", userIdKey, userId);
            await _js.InvokeVoidAsync("localStorage.setItem", usernameKey, username);

            _logger.LogDebug("成功保存认证信息到LocalStorage");
        }
        catch (Exception ex)
        {
            // LocalStorage可能不可用，但不影响当前会话的认证状态
            _logger.LogWarning(ex, "保存认证信息到LocalStorage失败，将仅在当前会话有效");
        }

        // 触发登录事件，通知订阅者执行后续操作（如建立SignalR连接）
        if (OnAuthenticated != null)
        {
            try
            {
                await OnAuthenticated.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行登录事件处理程序时发生错误");
            }
        }

        // 通知UI组件更新认证状态显示
        NotifyAuthStateChanged();
    }

    /// <summary>
    /// 服务端认证响应数据传输对象
    /// </summary>
    /// <param name="Token">JWT Token字符串</param>
    /// <param name="UserId">用户唯一标识符</param>
    /// <param name="Username">用户名</param>
    /// <param name="Email">用户邮箱</param>
    private record AuthResponse(string Token, Guid UserId, string Username, string Email);

    /// <summary>
    /// 服务端错误响应数据传输对象
    /// </summary>
    /// <param name="Message">错误消息</param>
    private record ErrorResponse(string Message);
}

/// <summary>
/// 认证操作结果类
/// 用于封装认证操作（注册、登录等）的执行结果
/// </summary>
public class AuthResult
{
    /// <summary>
    /// 获取操作是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 获取错误消息（仅在失败时有值）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 私有构造函数，防止外部直接实例化
    /// </summary>
    /// <param name="isSuccess">操作是否成功</param>
    /// <param name="errorMessage">错误消息（可选）</param>
    private AuthResult(bool isSuccess, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 创建表示成功的认证结果
    /// </summary>
    /// <returns>成功的认证结果对象</returns>
    public static AuthResult Success() => new(true);

    /// <summary>
    /// 创建表示失败的认证结果
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <returns>失败的认证结果对象，包含错误消息</returns>
    public static AuthResult Failure(string errorMessage) => new(false, errorMessage);
}
