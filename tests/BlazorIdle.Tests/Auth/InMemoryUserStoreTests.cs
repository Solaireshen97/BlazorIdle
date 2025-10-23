using BlazorIdle.Server.Auth.Models;
using BlazorIdle.Server.Auth.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorIdle.Tests.Auth;

/// <summary>
/// InMemoryUserStore的单元测试
/// 测试用户存储的各项功能，包括创建、查询、验证、令牌管理等
/// </summary>
public class InMemoryUserStoreTests
{
    private readonly IUserStore _userStore;
    private readonly Mock<ILogger<InMemoryUserStore>> _mockLogger;

    /// <summary>
    /// 测试初始化
    /// 为每个测试创建新的UserStore实例
    /// </summary>
    public InMemoryUserStoreTests()
    {
        _mockLogger = new Mock<ILogger<InMemoryUserStore>>();
        _userStore = new InMemoryUserStore(_mockLogger.Object);
    }

    #region 测试账户初始化

    /// <summary>
    /// 测试：构造函数应自动初始化3个测试账户
    /// </summary>
    [Fact]
    public async Task Constructor_ShouldInitializeTestAccounts()
    {
        // Act - 获取所有用户
        var users = await _userStore.GetAllUsersAsync();
        var userList = users.ToList();

        // Assert - 应该有3个用户
        Assert.Equal(3, userList.Count);
        
        // 验证测试账户存在
        Assert.Contains(userList, u => u.Username == "test1");
        Assert.Contains(userList, u => u.Username == "test2");
        Assert.Contains(userList, u => u.Username == "admin");
    }

    /// <summary>
    /// 测试：测试账户应该可以使用预设密码登录
    /// </summary>
    [Theory]
    [InlineData("test1", "password123", true)]
    [InlineData("test2", "password123", true)]
    [InlineData("admin", "admin123", true)]
    [InlineData("test1", "wrongpassword", false)]
    public async Task TestAccounts_ShouldValidateWithCorrectPassword(
        string username, string password, bool expectedResult)
    {
        // Act
        var result = await _userStore.ValidatePasswordAsync(username, password);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    #endregion

    #region 用户查询测试

    /// <summary>
    /// 测试：应该能够根据用户名获取用户
    /// </summary>
    [Fact]
    public async Task GetUserByUsernameAsync_ExistingUser_ShouldReturnUser()
    {
        // Arrange - 使用预设的测试账户
        var username = "test1";

        // Act
        var user = await _userStore.GetUserByUsernameAsync(username);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
        Assert.NotEmpty(user.Id);
        Assert.NotEmpty(user.PasswordHash);
    }

    /// <summary>
    /// 测试：查询不存在的用户应返回null
    /// </summary>
    [Fact]
    public async Task GetUserByUsernameAsync_NonExistingUser_ShouldReturnNull()
    {
        // Arrange
        var username = "nonexistent";

        // Act
        var user = await _userStore.GetUserByUsernameAsync(username);

        // Assert
        Assert.Null(user);
    }

    /// <summary>
    /// 测试：用户名查询应该不区分大小写
    /// </summary>
    [Theory]
    [InlineData("test1")]
    [InlineData("TEST1")]
    [InlineData("TesT1")]
    public async Task GetUserByUsernameAsync_DifferentCase_ShouldReturnSameUser(string username)
    {
        // Act
        var user = await _userStore.GetUserByUsernameAsync(username);

        // Assert
        Assert.NotNull(user);
        Assert.Equal("test1", user.Username); // 应该返回原始用户名
    }

    /// <summary>
    /// 测试：应该能够根据用户ID获取用户
    /// </summary>
    [Fact]
    public async Task GetUserByIdAsync_ExistingUser_ShouldReturnUser()
    {
        // Arrange - 先获取用户获得ID
        var user = await _userStore.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);

        // Act - 使用ID查询
        var userById = await _userStore.GetUserByIdAsync(user.Id);

        // Assert
        Assert.NotNull(userById);
        Assert.Equal(user.Id, userById.Id);
        Assert.Equal(user.Username, userById.Username);
    }

    #endregion

    #region 用户创建测试

    /// <summary>
    /// 测试：应该能够创建新用户
    /// </summary>
    [Fact]
    public async Task CreateUserAsync_NewUser_ShouldCreateSuccessfully()
    {
        // Arrange
        var username = "newuser";
        var password = "password123";

        // Act
        var user = await _userStore.CreateUserAsync(username, password);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
        Assert.NotEmpty(user.Id);
        Assert.NotEmpty(user.PasswordHash);
        Assert.NotEqual(password, user.PasswordHash); // 密码应该被哈希
        
        // 验证可以使用密码登录
        var isValid = await _userStore.ValidatePasswordAsync(username, password);
        Assert.True(isValid);
    }

    /// <summary>
    /// 测试：创建重复用户名应该抛出异常
    /// </summary>
    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_ShouldThrowException()
    {
        // Arrange
        var username = "test1"; // 已存在的用户名

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _userStore.CreateUserAsync(username, "password"));
    }

    /// <summary>
    /// 测试：创建用户名（不同大小写）重复应该抛出异常
    /// </summary>
    [Fact]
    public async Task CreateUserAsync_DuplicateUsernameDifferentCase_ShouldThrowException()
    {
        // Arrange
        var username = "TEST1"; // 与test1重复（不区分大小写）

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _userStore.CreateUserAsync(username, "password"));
    }

    /// <summary>
    /// 测试：新创建的用户应该有正确的时间戳
    /// </summary>
    [Fact]
    public async Task CreateUserAsync_NewUser_ShouldHaveCorrectTimestamps()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;

        // Act
        var user = await _userStore.CreateUserAsync("newuser", "password");

        // Assert
        var afterCreate = DateTime.UtcNow;
        Assert.InRange(user.CreatedAt, beforeCreate.AddSeconds(-1), afterCreate.AddSeconds(1));
        Assert.Null(user.LastLoginAt); // 新用户还未登录
    }

    #endregion

    #region 密码验证测试

    /// <summary>
    /// 测试：正确的密码应该验证通过
    /// </summary>
    [Fact]
    public async Task ValidatePasswordAsync_CorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var username = "testuser";
        var password = "correctpassword";
        await _userStore.CreateUserAsync(username, password);

        // Act
        var result = await _userStore.ValidatePasswordAsync(username, password);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// 测试：错误的密码应该验证失败
    /// </summary>
    [Fact]
    public async Task ValidatePasswordAsync_IncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var username = "testuser";
        await _userStore.CreateUserAsync(username, "correctpassword");

        // Act
        var result = await _userStore.ValidatePasswordAsync(username, "wrongpassword");

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// 测试：不存在的用户验证应该返回false
    /// </summary>
    [Fact]
    public async Task ValidatePasswordAsync_NonExistentUser_ShouldReturnFalse()
    {
        // Act
        var result = await _userStore.ValidatePasswordAsync("nonexistent", "password");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region 最后登录时间测试

    /// <summary>
    /// 测试：应该能够更新用户最后登录时间
    /// </summary>
    [Fact]
    public async Task UpdateLastLoginAsync_ShouldUpdateTimestamp()
    {
        // Arrange
        var user = await _userStore.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);
        var beforeUpdate = DateTime.UtcNow;

        // Act
        await _userStore.UpdateLastLoginAsync(user.Id);

        // Assert
        var updatedUser = await _userStore.GetUserByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.LastLoginAt);
        
        var afterUpdate = DateTime.UtcNow;
        Assert.InRange(updatedUser.LastLoginAt.Value, 
            beforeUpdate.AddSeconds(-1), 
            afterUpdate.AddSeconds(1));
    }

    #endregion

    #region 刷新令牌测试

    /// <summary>
    /// 测试：应该能够保存刷新令牌
    /// </summary>
    [Fact]
    public async Task SaveRefreshTokenAsync_ShouldSaveToken()
    {
        // Arrange
        var user = await _userStore.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);
        var refreshToken = "test_refresh_token";
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        await _userStore.SaveRefreshTokenAsync(user.Id, refreshToken, expiresAt);

        // Assert
        var updatedUser = await _userStore.GetUserByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(refreshToken, updatedUser.RefreshToken);
        Assert.Equal(expiresAt, updatedUser.RefreshTokenExpiresAt);
    }

    /// <summary>
    /// 测试：应该能够验证有效的刷新令牌
    /// </summary>
    [Fact]
    public async Task ValidateRefreshTokenAsync_ValidToken_ShouldReturnUser()
    {
        // Arrange
        var user = await _userStore.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);
        var refreshToken = "valid_refresh_token";
        var expiresAt = DateTime.UtcNow.AddDays(7);
        await _userStore.SaveRefreshTokenAsync(user.Id, refreshToken, expiresAt);

        // Act
        var validatedUser = await _userStore.ValidateRefreshTokenAsync(refreshToken);

        // Assert
        Assert.NotNull(validatedUser);
        Assert.Equal(user.Id, validatedUser.Id);
    }

    /// <summary>
    /// 测试：过期的刷新令牌应该验证失败
    /// </summary>
    [Fact]
    public async Task ValidateRefreshTokenAsync_ExpiredToken_ShouldReturnNull()
    {
        // Arrange
        var user = await _userStore.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);
        var refreshToken = "expired_refresh_token";
        var expiresAt = DateTime.UtcNow.AddDays(-1); // 已过期
        await _userStore.SaveRefreshTokenAsync(user.Id, refreshToken, expiresAt);

        // Act
        var validatedUser = await _userStore.ValidateRefreshTokenAsync(refreshToken);

        // Assert
        Assert.Null(validatedUser);
    }

    /// <summary>
    /// 测试：不存在的刷新令牌应该验证失败
    /// </summary>
    [Fact]
    public async Task ValidateRefreshTokenAsync_NonExistentToken_ShouldReturnNull()
    {
        // Act
        var validatedUser = await _userStore.ValidateRefreshTokenAsync("nonexistent_token");

        // Assert
        Assert.Null(validatedUser);
    }

    /// <summary>
    /// 测试：保存新令牌应该替换旧令牌
    /// </summary>
    [Fact]
    public async Task SaveRefreshTokenAsync_NewToken_ShouldReplaceOldToken()
    {
        // Arrange
        var user = await _userStore.GetUserByUsernameAsync("test1");
        Assert.NotNull(user);
        
        var oldToken = "old_token";
        var newToken = "new_token";
        var expiresAt = DateTime.UtcNow.AddDays(7);
        
        // 保存旧令牌
        await _userStore.SaveRefreshTokenAsync(user.Id, oldToken, expiresAt);

        // Act - 保存新令牌
        await _userStore.SaveRefreshTokenAsync(user.Id, newToken, expiresAt);

        // Assert - 旧令牌应该失效
        var oldTokenUser = await _userStore.ValidateRefreshTokenAsync(oldToken);
        Assert.Null(oldTokenUser);
        
        // 新令牌应该有效
        var newTokenUser = await _userStore.ValidateRefreshTokenAsync(newToken);
        Assert.NotNull(newTokenUser);
        Assert.Equal(user.Id, newTokenUser.Id);
    }

    #endregion

    #region 边界和异常测试

    /// <summary>
    /// 测试：GetAllUsersAsync应该返回所有用户
    /// </summary>
    [Fact]
    public async Task GetAllUsersAsync_ShouldReturnAllUsers()
    {
        // Arrange - 创建额外的用户
        await _userStore.CreateUserAsync("extra1", "password");
        await _userStore.CreateUserAsync("extra2", "password");

        // Act
        var users = await _userStore.GetAllUsersAsync();
        var userList = users.ToList();

        // Assert - 应该有5个用户（3个测试账户 + 2个新建）
        Assert.Equal(5, userList.Count);
    }

    #endregion
}
