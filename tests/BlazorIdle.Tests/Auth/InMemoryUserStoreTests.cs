using BlazorIdle.Server.Auth.Models;
using BlazorIdle.Server.Auth.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorIdle.Tests.Auth;

/// <summary>
/// InMemoryUserStore单元测试
/// 测试用户存储的各项功能
/// </summary>
public class InMemoryUserStoreTests
{
    private readonly Mock<ILogger<InMemoryUserStore>> _mockLogger;
    private readonly IConfiguration _configuration;

    public InMemoryUserStoreTests()
    {
        _mockLogger = new Mock<ILogger<InMemoryUserStore>>();
        
        // 创建测试配置
        var configData = new Dictionary<string, string>
        {
            { "Auth:BCryptWorkFactor", "12" },
            { "Auth:TestAccounts:0:Username", "test1" },
            { "Auth:TestAccounts:0:Password", "password123" },
            { "Auth:TestAccounts:1:Username", "test2" },
            { "Auth:TestAccounts:1:Password", "password123" },
            { "Auth:TestAccounts:2:Username", "admin" },
            { "Auth:TestAccounts:2:Password", "admin123" }
        };
        
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();
    }

    /// <summary>
    /// 创建UserStore实例
    /// </summary>
    private InMemoryUserStore CreateUserStore()
    {
        return new InMemoryUserStore(_mockLogger.Object, _configuration);
    }

    [Fact]
    public async Task InitializeTestAccounts_ShouldCreateDefaultAccounts()
    {
        // Arrange & Act
        var store = CreateUserStore();

        // Assert
        var allUsers = await store.GetAllUsersAsync();
        Assert.Equal(3, allUsers.Count());
    }

    [Fact]
    public async Task GetUserByUsername_WithExistingUser_ShouldReturnUser()
    {
        // Arrange
        var store = CreateUserStore();

        // Act
        var user = await store.GetUserByUsernameAsync("test1");

        // Assert
        Assert.NotNull(user);
        Assert.Equal("test1", user.Username);
    }

    [Fact]
    public async Task GetUserByUsername_WithNonExistingUser_ShouldReturnNull()
    {
        // Arrange
        var store = CreateUserStore();

        // Act
        var user = await store.GetUserByUsernameAsync("nonexistent");

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetUserByUsername_ShouldBeCaseInsensitive()
    {
        // Arrange
        var store = CreateUserStore();

        // Act
        var user1 = await store.GetUserByUsernameAsync("TEST1");
        var user2 = await store.GetUserByUsernameAsync("test1");
        var user3 = await store.GetUserByUsernameAsync("TeSt1");

        // Assert
        Assert.NotNull(user1);
        Assert.NotNull(user2);
        Assert.NotNull(user3);
        Assert.Equal(user1.Id, user2.Id);
        Assert.Equal(user2.Id, user3.Id);
    }

    [Fact]
    public async Task GetUserById_WithExistingUser_ShouldReturnUser()
    {
        // Arrange
        var store = CreateUserStore();
        var originalUser = await store.GetUserByUsernameAsync("test1");
        Assert.NotNull(originalUser);

        // Act
        var user = await store.GetUserByIdAsync(originalUser.Id);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(originalUser.Id, user.Id);
        Assert.Equal(originalUser.Username, user.Username);
    }

    [Fact]
    public async Task GetUserById_WithNonExistingUser_ShouldReturnNull()
    {
        // Arrange
        var store = CreateUserStore();

        // Act
        var user = await store.GetUserByIdAsync("nonexistent-id");

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task CreateUserAsync_WithNewUsername_ShouldCreateUser()
    {
        // Arrange
        var store = CreateUserStore();
        var username = "newuser";
        var password = "password123";

        // Act
        var user = await store.CreateUserAsync(username, password);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
        Assert.NotEmpty(user.Id);
        Assert.NotEmpty(user.PasswordHash);
        Assert.NotEqual(password, user.PasswordHash); // 密码应该被哈希
    }

    [Fact]
    public async Task CreateUserAsync_WithExistingUsername_ShouldThrowException()
    {
        // Arrange
        var store = CreateUserStore();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await store.CreateUserAsync("test1", "password");
        });
    }

    [Fact]
    public async Task ValidatePasswordAsync_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var store = CreateUserStore();

        // Act
        var isValid = await store.ValidatePasswordAsync("test1", "password123");

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidatePasswordAsync_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var store = CreateUserStore();

        // Act
        var isValid = await store.ValidatePasswordAsync("test1", "wrongpassword");

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidatePasswordAsync_WithNonExistingUser_ShouldReturnFalse()
    {
        // Arrange
        var store = CreateUserStore();

        // Act
        var isValid = await store.ValidatePasswordAsync("nonexistent", "password");

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task UpdateLastLoginAsync_ShouldUpdateTimestamp()
    {
        // Arrange
        var store = CreateUserStore();
        var user = await store.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);
        var originalLastLogin = user.LastLoginAt;

        // Act
        await Task.Delay(100); // 确保时间戳不同
        await store.UpdateLastLoginAsync(user.Id);

        // Assert
        var updatedUser = await store.GetUserByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.LastLoginAt);
        Assert.NotEqual(originalLastLogin, updatedUser.LastLoginAt);
    }

    [Fact]
    public async Task SaveRefreshTokenAsync_ShouldSaveTokenAndExpiration()
    {
        // Arrange
        var store = CreateUserStore();
        var user = await store.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);
        var refreshToken = "test-refresh-token";
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        await store.SaveRefreshTokenAsync(user.Id, refreshToken, expiresAt);

        // Assert
        var updatedUser = await store.GetUserByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(refreshToken, updatedUser.RefreshToken);
        Assert.Equal(expiresAt, updatedUser.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task SaveRefreshTokenAsync_ShouldReplaceOldToken()
    {
        // Arrange
        var store = CreateUserStore();
        var user = await store.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);
        var oldToken = "old-token";
        var newToken = "new-token";
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        await store.SaveRefreshTokenAsync(user.Id, oldToken, expiresAt);
        await store.SaveRefreshTokenAsync(user.Id, newToken, expiresAt);

        // Assert
        var updatedUser = await store.GetUserByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(newToken, updatedUser.RefreshToken);
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithValidToken_ShouldReturnUser()
    {
        // Arrange
        var store = CreateUserStore();
        var user = await store.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);
        var refreshToken = "valid-token";
        var expiresAt = DateTime.UtcNow.AddDays(7);
        await store.SaveRefreshTokenAsync(user.Id, refreshToken, expiresAt);

        // Act
        var validatedUser = await store.ValidateRefreshTokenAsync(refreshToken);

        // Assert
        Assert.NotNull(validatedUser);
        Assert.Equal(user.Id, validatedUser.Id);
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithExpiredToken_ShouldReturnNull()
    {
        // Arrange
        var store = CreateUserStore();
        var user = await store.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);
        var refreshToken = "expired-token";
        var expiresAt = DateTime.UtcNow.AddSeconds(-1); // 已过期
        await store.SaveRefreshTokenAsync(user.Id, refreshToken, expiresAt);

        // Act
        await Task.Delay(10); // 确保令牌确实过期
        var validatedUser = await store.ValidateRefreshTokenAsync(refreshToken);

        // Assert
        Assert.Null(validatedUser);
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithInvalidToken_ShouldReturnNull()
    {
        // Arrange
        var store = CreateUserStore();

        // Act
        var validatedUser = await store.ValidateRefreshTokenAsync("invalid-token");

        // Assert
        Assert.Null(validatedUser);
    }

    [Fact]
    public async Task GetAllUsersAsync_ShouldReturnAllUsers()
    {
        // Arrange
        var store = CreateUserStore();

        // Act
        var users = await store.GetAllUsersAsync();

        // Assert
        Assert.NotNull(users);
        Assert.True(users.Count() >= 3); // 至少包含3个测试账户
    }

    [Fact]
    public async Task PasswordHash_ShouldUseBCrypt()
    {
        // Arrange
        var store = CreateUserStore();
        var password = "testpassword";

        // Act
        var user = await store.CreateUserAsync("hashtest", password);

        // Assert
        Assert.NotNull(user.PasswordHash);
        Assert.StartsWith("$2a$", user.PasswordHash); // BCrypt哈希特征
        Assert.True(BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
    }

    [Fact]
    public async Task CreatedAt_ShouldBeSetOnUserCreation()
    {
        // Arrange
        var store = CreateUserStore();
        var beforeCreate = DateTime.UtcNow;

        // Act
        var user = await store.CreateUserAsync("timetest", "password");
        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.True(user.CreatedAt >= beforeCreate);
        Assert.True(user.CreatedAt <= afterCreate);
    }
}
