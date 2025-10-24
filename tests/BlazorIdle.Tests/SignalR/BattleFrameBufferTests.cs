using BlazorIdle.Server.Infrastructure.SignalR.Services;
using BlazorIdle.Shared.Messages.Battle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BlazorIdle.Tests.SignalR;

/// <summary>
/// BattleFrameBuffer单元测试
/// 测试战斗帧缓冲区的核心功能
/// </summary>
public class BattleFrameBufferTests
{
    #region 构造函数和配置测试

    [Fact]
    public void Constructor_WithDefaultSize_ShouldSucceed()
    {
        // Act
        var buffer = new BattleFrameBuffer();

        // Assert
        Assert.NotNull(buffer);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Constructor_WithCustomSize_ShouldSucceed()
    {
        // Act
        var buffer = new BattleFrameBuffer(maxSize: 100);

        // Assert
        Assert.NotNull(buffer);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Constructor_WithOptions_ShouldSucceed()
    {
        // Arrange
        var options = new BattleFrameBufferOptions { MaxSize = 200 };

        // Act
        var buffer = new BattleFrameBuffer(options);

        // Assert
        Assert.NotNull(buffer);
    }

    [Fact]
    public void Constructor_WithInvalidSize_ShouldThrowException()
    {
        // Arrange
        var options = new BattleFrameBufferOptions { MaxSize = 0 };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new BattleFrameBuffer(options));
    }

    #endregion

    #region AddFrame测试

    [Fact]
    public void AddFrame_WithValidFrame_ShouldStoreFrame()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);
        var frame = CreateTestFrame(version: 1);

        // Act
        buffer.AddFrame(frame);

        // Assert
        Assert.Equal(1, buffer.Count);
        var retrieved = buffer.GetFrame(1);
        Assert.NotNull(retrieved);
        Assert.Equal(1, retrieved.Version);
    }

    [Fact]
    public void AddFrame_WithNullFrame_ShouldThrowException()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => buffer.AddFrame(null!));
    }

    [Fact]
    public void AddFrame_MultipleFrames_ShouldStoreAll()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);
        var frames = Enumerable.Range(1, 10)
            .Select(v => CreateTestFrame(version: v))
            .ToList();

        // Act
        foreach (var frame in frames)
        {
            buffer.AddFrame(frame);
        }

        // Assert
        Assert.Equal(10, buffer.Count);
        Assert.Equal(1, buffer.MinVersion);
        Assert.Equal(10, buffer.MaxVersion);
    }

    [Fact]
    public void AddFrame_ExceedMaxSize_ShouldRemoveOldFrames()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(maxSize: 10);

        // Act - 添加20个帧
        for (long i = 1; i <= 20; i++)
        {
            buffer.AddFrame(CreateTestFrame(version: i));
        }

        // Assert
        Assert.Equal(10, buffer.Count);
        Assert.Equal(11, buffer.MinVersion); // 前10个已被清理
        Assert.Equal(20, buffer.MaxVersion);
        
        // 验证旧帧已被移除
        Assert.Null(buffer.GetFrame(5));
        // 验证新帧仍然存在
        Assert.NotNull(buffer.GetFrame(15));
    }

    #endregion

    #region GetFrame测试

    [Fact]
    public void GetFrame_ExistingFrame_ShouldReturnFrame()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);
        var frame = CreateTestFrame(version: 5);
        buffer.AddFrame(frame);

        // Act
        var retrieved = buffer.GetFrame(5);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(5, retrieved.Version);
        Assert.Equal("test-battle", retrieved.BattleId);
    }

    [Fact]
    public void GetFrame_NonExistingFrame_ShouldReturnNull()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);

        // Act
        var retrieved = buffer.GetFrame(999);

        // Assert
        Assert.Null(retrieved);
    }

    #endregion

    #region GetFrames测试

    [Fact]
    public void GetFrames_ConsecutiveFrames_ShouldReturnAllFrames()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);
        for (long i = 1; i <= 10; i++)
        {
            buffer.AddFrame(CreateTestFrame(version: i));
        }

        // Act
        var frames = buffer.GetFrames(3, 7);

        // Assert
        Assert.Equal(5, frames.Count);
        Assert.Equal(3, frames[0].Version);
        Assert.Equal(4, frames[1].Version);
        Assert.Equal(7, frames[4].Version);
    }

    [Fact]
    public void GetFrames_WithMissingFrames_ShouldReturnEmptyList()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);
        buffer.AddFrame(CreateTestFrame(version: 1));
        buffer.AddFrame(CreateTestFrame(version: 3)); // 跳过版本2
        buffer.AddFrame(CreateTestFrame(version: 4));

        // Act
        var frames = buffer.GetFrames(1, 4);

        // Assert
        Assert.Empty(frames); // 因为缺少版本2
    }

    [Fact]
    public void GetFrames_OutOfRange_ShouldReturnEmptyList()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(maxSize: 10);
        for (long i = 11; i <= 20; i++)
        {
            buffer.AddFrame(CreateTestFrame(version: i));
        }

        // Act - 请求已被清理的帧
        var frames = buffer.GetFrames(1, 10);

        // Assert
        Assert.Empty(frames);
    }

    [Fact]
    public void GetFrames_InvalidRange_ShouldReturnEmptyList()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);

        // Act - from > to
        var frames = buffer.GetFrames(10, 5);

        // Assert
        Assert.Empty(frames);
    }

    #endregion

    #region HasFrame测试

    [Fact]
    public void HasFrame_ExistingFrame_ShouldReturnTrue()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);
        buffer.AddFrame(CreateTestFrame(version: 5));

        // Act
        var hasFrame = buffer.HasFrame(5);

        // Assert
        Assert.True(hasFrame);
    }

    [Fact]
    public void HasFrame_NonExistingFrame_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);

        // Act
        var hasFrame = buffer.HasFrame(999);

        // Assert
        Assert.False(hasFrame);
    }

    #endregion

    #region HasCompleteRange测试

    [Fact]
    public void HasCompleteRange_CompleteRange_ShouldReturnTrue()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);
        for (long i = 1; i <= 10; i++)
        {
            buffer.AddFrame(CreateTestFrame(version: i));
        }

        // Act
        var hasComplete = buffer.HasCompleteRange(3, 7);

        // Assert
        Assert.True(hasComplete);
    }

    [Fact]
    public void HasCompleteRange_IncompleteRange_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);
        buffer.AddFrame(CreateTestFrame(version: 1));
        buffer.AddFrame(CreateTestFrame(version: 3)); // 缺少2

        // Act
        var hasComplete = buffer.HasCompleteRange(1, 3);

        // Assert
        Assert.False(hasComplete);
    }

    [Fact]
    public void HasCompleteRange_OutOfRange_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);
        buffer.AddFrame(CreateTestFrame(version: 5));

        // Act
        var hasComplete = buffer.HasCompleteRange(1, 10);

        // Assert
        Assert.False(hasComplete);
    }

    #endregion

    #region GetStatistics测试

    [Fact]
    public void GetStatistics_EmptyBuffer_ShouldReturnCorrectStats()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);

        // Act
        var stats = buffer.GetStatistics();

        // Assert
        Assert.Equal(0, stats.CurrentSize);
        Assert.Equal(0, stats.MinVersion);
        Assert.Equal(0, stats.MaxVersion);
        Assert.Equal(100, stats.MaxSize);
    }

    [Fact]
    public void GetStatistics_WithFrames_ShouldReturnCorrectStats()
    {
        // Arrange
        var options = new BattleFrameBufferOptions
        {
            MaxSize = 100,
            EnableStatistics = true
        };
        var buffer = new BattleFrameBuffer(options);

        for (long i = 1; i <= 10; i++)
        {
            buffer.AddFrame(CreateTestFrame(version: i));
        }

        // Act
        var stats = buffer.GetStatistics();

        // Assert
        Assert.Equal(10, stats.CurrentSize);
        Assert.Equal(1, stats.MinVersion);
        Assert.Equal(10, stats.MaxVersion);
        Assert.Equal(10, stats.TotalFramesAdded);
    }

    [Fact]
    public void GetStatistics_WithQueries_ShouldTrackQueryStats()
    {
        // Arrange
        var options = new BattleFrameBufferOptions
        {
            MaxSize = 100,
            EnableStatistics = true
        };
        var buffer = new BattleFrameBuffer(options);

        for (long i = 11; i <= 20; i++)
        {
            buffer.AddFrame(CreateTestFrame(version: i));
        }

        // Act
        buffer.GetFrames(13, 17); // 成功查询
        buffer.GetFrames(1, 5); // 超出范围查询（太旧）

        var stats = buffer.GetStatistics();

        // Assert
        Assert.Equal(1, stats.SuccessfulQueries);
        Assert.Equal(1, stats.OutOfRangeQueries);
    }

    #endregion

    #region Clear测试

    [Fact]
    public void Clear_ShouldRemoveAllFrames()
    {
        // Arrange
        var buffer = new BattleFrameBuffer(100);
        for (long i = 1; i <= 10; i++)
        {
            buffer.AddFrame(CreateTestFrame(version: i));
        }

        // Act
        buffer.Clear();

        // Assert
        Assert.Equal(0, buffer.Count);
        Assert.Equal(0, buffer.MinVersion);
        Assert.Equal(0, buffer.MaxVersion);
    }

    #endregion

    #region 辅助方法

    private FrameTick CreateTestFrame(long version, string battleId = "test-battle")
    {
        return new FrameTick
        {
            Version = version,
            BattleId = battleId,
            ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Phase = BattlePhase.Active,
            Metrics = new FrameMetrics
            {
                Health = new HealthMetrics
                {
                    Current = 100,
                    Max = 100,
                    Delta = 0
                }
            }
        };
    }

    #endregion
}
