using System.Collections.Concurrent;
using BlazorIdle.Server.Auth.Models;

namespace BlazorIdle.Server.Auth.Services;

/// <summary>
/// 基于内存的用户存储实现
/// 用于开发和测试，数据在服务重启后会丢失
/// 使用ConcurrentDictionary实现线程安全的数据访问
/// </summary>
public class InMemoryUserStore : IUserStore
{
    // 使用用户ID作为键存储用户实体
    private readonly ConcurrentDictionary<string, User> _usersById = new();
    
    // 使用用户名（小写）作为键映射到用户ID，用于快速按用户名查找
    private readonly ConcurrentDictionary<string, string> _usernameToId = new();
    
    // 使用刷新令牌作为键映射到用户ID，用于验证刷新令牌
    private readonly ConcurrentDictionary<string, string> _refreshTokenToUserId = new();
    
    private readonly ILogger<InMemoryUserStore> _logger;

    /// <summary>
    /// 构造函数
    /// 初始化用户存储并创建预设的测试账户
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public InMemoryUserStore(ILogger<InMemoryUserStore> logger)
    {
        _logger = logger;
        InitializeTestAccounts();
    }

    /// <summary>
    /// 初始化测试账户
    /// 创建三个预设测试账户用于开发和测试
    /// </summary>
    private void InitializeTestAccounts()
    {
        _logger.LogInformation("初始化测试账户...");
        
        // 创建3个测试账户
        // test1和test2使用相同密码，便于测试
        CreateTestUser("test1", "password123");
        CreateTestUser("test2", "password123");
        // admin账户使用不同密码
        CreateTestUser("admin", "admin123");
        
        _logger.LogInformation("测试账户初始化完成，共 {Count} 个账户", _usersById.Count);
    }

    /// <summary>
    /// 创建测试用户的辅助方法
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">明文密码</param>
    private void CreateTestUser(string username, string password)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            // 使用BCrypt进行密码哈希，工作因子为12
            // 工作因子越高，哈希计算越慢，安全性越高，但也会影响性能
            // 12是一个在安全性和性能之间较好的平衡点
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            CreatedAt = DateTime.UtcNow
        };

        _usersById[user.Id] = user;
        // 用户名转为小写存储，实现不区分大小写的用户名查找
        _usernameToId[username.ToLowerInvariant()] = user.Id;
        
        _logger.LogDebug("创建测试账户：{Username} (ID: {UserId})", username, user.Id);
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
    /// 根据用户名获取用户
    /// 用户名查找不区分大小写
    /// </summary>
    public Task<User?> GetUserByUsernameAsync(string username)
    {
        // 将用户名转为小写进行查找
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
    /// <exception cref="InvalidOperationException">当用户名已存在时抛出</exception>
    public Task<User> CreateUserAsync(string username, string password)
    {
        var normalizedUsername = username.ToLowerInvariant();
        
        // 检查用户名是否已存在
        if (_usernameToId.ContainsKey(normalizedUsername))
        {
            throw new InvalidOperationException($"用户名 '{username}' 已存在");
        }

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            // 使用BCrypt哈希密码，工作因子12
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            CreatedAt = DateTime.UtcNow
        };

        _usersById[user.Id] = user;
        _usernameToId[normalizedUsername] = user.Id;
        
        _logger.LogInformation("创建新用户：{Username} (ID: {UserId})", username, user.Id);
        
        return Task.FromResult(user);
    }

    /// <summary>
    /// 验证用户密码
    /// 使用BCrypt.Verify方法验证明文密码与哈希密码是否匹配
    /// </summary>
    public async Task<bool> ValidatePasswordAsync(string username, string password)
    {
        var user = await GetUserByUsernameAsync(username);
        if (user == null)
        {
            return false;
        }

        try
        {
            // BCrypt.Verify会自动处理盐值和哈希比对
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
    /// 将新的刷新令牌保存到用户记录，并更新令牌映射
    /// </summary>
    public Task SaveRefreshTokenAsync(string userId, string refreshToken, DateTime expiresAt)
    {
        if (_usersById.TryGetValue(userId, out var user))
        {
            // 如果用户已有刷新令牌，先从映射中移除旧的令牌
            if (!string.IsNullOrEmpty(user.RefreshToken))
            {
                _refreshTokenToUserId.TryRemove(user.RefreshToken, out _);
            }

            // 保存新的刷新令牌到用户记录
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiresAt = expiresAt;
            
            // 在映射中添加新的令牌到用户ID的映射
            _refreshTokenToUserId[refreshToken] = userId;
            
            _logger.LogDebug("保存用户 {UserId} 的刷新令牌", userId);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 验证刷新令牌
    /// 检查令牌是否存在且未过期
    /// </summary>
    public Task<User?> ValidateRefreshTokenAsync(string refreshToken)
    {
        // 通过刷新令牌查找对应的用户ID
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
    /// 获取所有用户
    /// 仅用于测试和调试，生产环境应限制访问
    /// </summary>
    public Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return Task.FromResult<IEnumerable<User>>(_usersById.Values.ToList());
    }
}
