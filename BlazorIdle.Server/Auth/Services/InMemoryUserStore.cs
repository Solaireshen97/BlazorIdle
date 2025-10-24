using System.Collections.Concurrent;
using BlazorIdle.Server.Auth.Models;

namespace BlazorIdle.Server.Auth.Services;

/// <summary>
/// 基于内存的用户存储实现
/// 用于开发和测试，数据在服务重启后会丢失
/// 生产环境应替换为基于数据库的实现
/// </summary>
public class InMemoryUserStore : IUserStore
{
    // 使用ConcurrentDictionary确保线程安全
    private readonly ConcurrentDictionary<string, User> _usersById = new();
    private readonly ConcurrentDictionary<string, string> _usernameToId = new();
    private readonly ConcurrentDictionary<string, string> _refreshTokenToUserId = new();
    
    private readonly ILogger<InMemoryUserStore> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="configuration">配置对象，用于读取测试账户信息</param>
    public InMemoryUserStore(
        ILogger<InMemoryUserStore> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        InitializeTestAccounts();
    }

    /// <summary>
    /// 初始化测试账户
    /// 从配置文件读取测试账户信息，如果配置不存在则使用默认值
    /// </summary>
    private void InitializeTestAccounts()
    {
        _logger.LogInformation("初始化测试账户...");
        
        // 从配置读取测试账户，如果没有配置则使用默认值
        var testAccounts = _configuration.GetSection("Auth:TestAccounts").Get<TestAccountConfig[]>() 
            ?? GetDefaultTestAccounts();
        
        foreach (var account in testAccounts)
        {
            CreateTestUser(account.Username, account.Password);
        }
        
        _logger.LogInformation("测试账户初始化完成，共 {Count} 个账户", _usersById.Count);
    }

    /// <summary>
    /// 获取默认测试账户配置
    /// </summary>
    private TestAccountConfig[] GetDefaultTestAccounts()
    {
        return new[]
        {
            new TestAccountConfig { Username = "test1", Password = "password123" },
            new TestAccountConfig { Username = "test2", Password = "password123" },
            new TestAccountConfig { Username = "admin", Password = "admin123" }
        };
    }

    /// <summary>
    /// 创建测试用户
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">明文密码</param>
    private void CreateTestUser(string username, string password)
    {
        try
        {
            // 从配置读取BCrypt工作因子，默认为12
            var workFactor = _configuration.GetValue<int>("Auth:BCryptWorkFactor", 12);
            
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor),
                CreatedAt = DateTime.UtcNow
            };

            _usersById[user.Id] = user;
            _usernameToId[username.ToLowerInvariant()] = user.Id;
            
            _logger.LogDebug("创建测试账户：{Username} (ID: {UserId})", username, user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建测试账户失败：{Username}", username);
        }
    }

    /// <summary>
    /// 根据用户ID获取用户
    /// </summary>
    public Task<User?> GetUserByIdAsync(string userId)
    {
        _usersById.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    /// <summary>
    /// 根据用户名获取用户（不区分大小写）
    /// </summary>
    public Task<User?> GetUserByUsernameAsync(string username)
    {
        var normalizedUsername = username.ToLowerInvariant();
        if (_usernameToId.TryGetValue(normalizedUsername, out var userId))
        {
            _usersById.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }
        return Task.FromResult<User?>(null);
    }

    /// <summary>
    /// 创建新用户
    /// </summary>
    /// <exception cref="InvalidOperationException">用户名已存在</exception>
    public Task<User> CreateUserAsync(string username, string password)
    {
        var normalizedUsername = username.ToLowerInvariant();
        
        // 检查用户名是否已存在
        if (_usernameToId.ContainsKey(normalizedUsername))
        {
            throw new InvalidOperationException($"用户名 '{username}' 已存在");
        }

        // 从配置读取BCrypt工作因子
        var workFactor = _configuration.GetValue<int>("Auth:BCryptWorkFactor", 12);
        
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor),
            CreatedAt = DateTime.UtcNow
        };

        _usersById[user.Id] = user;
        _usernameToId[normalizedUsername] = user.Id;
        
        _logger.LogInformation("创建新用户：{Username} (ID: {UserId})", username, user.Id);
        
        return Task.FromResult(user);
    }

    /// <summary>
    /// 验证用户密码
    /// </summary>
    public async Task<bool> ValidatePasswordAsync(string username, string password)
    {
        var user = await GetUserByUsernameAsync(username);
        if (user == null)
        {
            // 防止时序攻击：即使用户不存在也执行哈希验证
            BCrypt.Net.BCrypt.Verify(password, "$2a$12$dummy.hash.for.timing.attack.prevention");
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "密码验证失败：{Username}", username);
            return false;
        }
    }

    /// <summary>
    /// 更新用户最后登录时间
    /// </summary>
    public Task UpdateLastLoginAsync(string userId)
    {
        if (_usersById.TryGetValue(userId, out var user))
        {
            user.LastLoginAt = DateTime.UtcNow;
            _logger.LogDebug("更新用户 {UserId} 的最后登录时间", userId);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 保存刷新令牌
    /// </summary>
    public Task SaveRefreshTokenAsync(string userId, string refreshToken, DateTime expiresAt)
    {
        if (_usersById.TryGetValue(userId, out var user))
        {
            // 移除旧的刷新令牌映射
            if (!string.IsNullOrEmpty(user.RefreshToken))
            {
                _refreshTokenToUserId.TryRemove(user.RefreshToken, out _);
            }

            // 保存新的刷新令牌
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiresAt = expiresAt;
            _refreshTokenToUserId[refreshToken] = userId;
            
            _logger.LogDebug("保存用户 {UserId} 的刷新令牌，过期时间：{ExpiresAt}", userId, expiresAt);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 验证刷新令牌
    /// </summary>
    public Task<User?> ValidateRefreshTokenAsync(string refreshToken)
    {
        if (_refreshTokenToUserId.TryGetValue(refreshToken, out var userId))
        {
            if (_usersById.TryGetValue(userId, out var user))
            {
                // 检查刷新令牌是否过期
                if (user.RefreshTokenExpiresAt.HasValue && 
                    user.RefreshTokenExpiresAt.Value > DateTime.UtcNow)
                {
                    return Task.FromResult<User?>(user);
                }
                
                _logger.LogWarning("用户 {UserId} 的刷新令牌已过期", userId);
            }
        }
        
        return Task.FromResult<User?>(null);
    }

    /// <summary>
    /// 获取所有用户（仅供测试）
    /// </summary>
    public Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return Task.FromResult<IEnumerable<User>>(_usersById.Values.ToList());
    }
}

/// <summary>
/// 测试账户配置类
/// </summary>
public class TestAccountConfig
{
    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
