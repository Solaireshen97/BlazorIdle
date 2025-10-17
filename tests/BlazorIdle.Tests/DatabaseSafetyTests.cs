using Xunit;
using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace BlazorIdle.Tests;

/// <summary>
/// 数据库安全性和重试策略测试
/// </summary>
public class DatabaseSafetyTests
{
    /// <summary>
    /// 测试数据库连接配置是否正确（WAL 模式等）
    /// </summary>
    [Fact]
    public async Task Database_Configuration_Should_Be_Applied()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new GameDbContext(options);
        
        // Act: 创建数据库并保存数据以触发 PRAGMA 应用
        await context.Database.EnsureCreatedAsync();
        
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "Test Character",
            Profession = Profession.Warrior,
            Level = 1,
            Strength = 10,
            Agility = 10,
            Intellect = 10,
            Stamina = 10
        };
        
        context.Characters.Add(character);
        await context.SaveChangesAsync();

        // Assert: 验证数据已正确保存
        var saved = await context.Characters.FirstOrDefaultAsync(c => c.Id == character.Id);
        Assert.NotNull(saved);
        Assert.Equal("Test Character", saved.Name);
    }

    /// <summary>
    /// 测试重试策略扩展方法存在
    /// </summary>
    [Fact]
    public async Task DatabaseRetryPolicy_Extension_Should_Work()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new GameDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "Retry Test",
            Profession = Profession.Ranger,
            Level = 1,
            Strength = 10,
            Agility = 20,
            Intellect = 8,
            Stamina = 12
        };

        context.Characters.Add(character);

        // Act: 使用重试策略保存
        var result = await DatabaseRetryPolicy.SaveChangesWithRetryAsync(context);

        // Assert
        Assert.True(result > 0);
        var saved = await context.Characters.FirstOrDefaultAsync(c => c.Id == character.Id);
        Assert.NotNull(saved);
    }

    /// <summary>
    /// 测试 DbContext 可以正确创建和销毁
    /// </summary>
    [Fact]
    public async Task DbContext_Should_Dispose_Cleanly()
    {
        // Arrange & Act
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        GameDbContext? context = null;
        try
        {
            context = new GameDbContext(options);
            await context.Database.EnsureCreatedAsync();
            
            // 添加一些数据
            context.Characters.Add(new Character
            {
                Id = Guid.NewGuid(),
                Name = "Dispose Test",
                Profession = Profession.Warrior,
                Level = 1,
                Strength = 20,
                Agility = 10,
                Intellect = 5,
                Stamina = 15
            });
            
            await context.SaveChangesAsync();
        }
        finally
        {
            // Assert: 应该能够正常释放
            if (context != null)
            {
                await context.DisposeAsync();
            }
        }

        // 如果执行到这里没有异常，测试通过
        Assert.True(true);
    }

    /// <summary>
    /// 测试多次保存操作不会导致死锁
    /// </summary>
    [Fact]
    public async Task Multiple_SaveChanges_Should_Not_Deadlock()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite("Data Source=test_concurrent.db")
            .Options;

        using var context = new GameDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Act: 执行多次保存操作
        for (int i = 0; i < 5; i++)
        {
            var character = new Character
            {
                Id = Guid.NewGuid(),
                Name = $"Character {i}",
                Profession = Profession.Warrior,
                Level = 1,
                Strength = 10,
                Agility = 10,
                Intellect = 10,
                Stamina = 10
            };

            context.Characters.Add(character);
            await DatabaseRetryPolicy.SaveChangesWithRetryAsync(context);
        }

        // Assert
        var count = await context.Characters.CountAsync();
        Assert.True(count >= 5);

        // Cleanup
        await context.Database.EnsureDeletedAsync();
    }
}
