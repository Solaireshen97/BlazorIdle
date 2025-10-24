using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Infrastructure.SignalR;
using BlazorIdle.Server.Infrastructure.SignalR.Broadcasters;
using BlazorIdle.Server.Infrastructure.SignalR.Services;
using BlazorIdle.Server.Infrastructure.SignalR.Models;
using BlazorIdle.Shared.Messages.Battle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BlazorIdle.Tests.SignalR;

/// <summary>
/// CombatBroadcaster单元测试
/// 测试战斗帧广播服务的核心功能
/// </summary>
public class CombatBroadcasterTests : IDisposable
{
    private readonly Mock<ISignalRDispatcher> _mockDispatcher;
    private readonly Mock<StepBattleCoordinator> _mockCoordinator;
    private readonly Mock<ILogger<CombatBroadcaster>> _mockLogger;
    private readonly CombatBroadcasterOptions _options;
    private readonly BattleFrameBufferOptions _bufferOptions;
    private readonly CombatBroadcaster _broadcaster;

    public CombatBroadcasterTests()
    {
        _mockDispatcher = new Mock<ISignalRDispatcher>();
        _mockCoordinator = new Mock<StepBattleCoordinator>();
        _mockLogger = new Mock<ILogger<CombatBroadcaster>>();
        
        // 使用默认配置
        _options = new CombatBroadcasterOptions
        {
            TickIntervalMs = 10,
            DefaultFrequency = 8,
            MinFrequency = 2,
            MaxFrequency = 10,
            SnapshotIntervalFrames = 300,
            AutoCleanupFinishedBattles = true,
            CleanupDelaySeconds = 5,
            MaxConcurrentBattles = 0,
            EnableDetailedLogging = false
        };

        _bufferOptions = new BattleFrameBufferOptions
        {
            MaxSize = 300,
            EnableStatistics = false,
            CompactOnCleanup = false,
            CleanupThreshold = 0
        };

        _broadcaster = new CombatBroadcaster(
            _mockDispatcher.Object,
            _mockCoordinator.Object,
            _mockLogger.Object,
            Options.Create(_options),
            Options.Create(_bufferOptions));
    }

    public void Dispose()
    {
        _broadcaster?.Dispose();
    }

    #region 基础功能测试

    [Fact]
    public void Constructor_WithValidParameters_ShouldSucceed()
    {
        // Act & Assert
        Assert.NotNull(_broadcaster);
    }

    [Fact]
    public void Constructor_WithNullDispatcher_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CombatBroadcaster(null!, _mockCoordinator.Object, _mockLogger.Object, Options.Create(_options), Options.Create(_bufferOptions)));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CombatBroadcaster(_mockDispatcher.Object, _mockCoordinator.Object, null!, Options.Create(_options), Options.Create(_bufferOptions)));
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CombatBroadcaster(_mockDispatcher.Object, _mockCoordinator.Object, _mockLogger.Object, null!, Options.Create(_bufferOptions)));
    }

    #endregion

    #region StartBroadcast测试

    [Fact]
    public void StartBroadcast_WithValidBattleId_ShouldAddToActiveBattles()
    {
        // Arrange
        var battleId = "battle-123";

        // Act
        _broadcaster.StartBroadcast(battleId);

        // Assert
        Assert.Equal(1, _broadcaster.GetActiveBattleCount());
        var config = _broadcaster.GetBattleConfig(battleId);
        Assert.NotNull(config);
        Assert.Equal(_options.DefaultFrequency, config.Frequency);
    }

    [Fact]
    public void StartBroadcast_WithCustomFrequency_ShouldUseCustomFrequency()
    {
        // Arrange
        var battleId = "battle-123";
        var customFrequency = 6;

        // Act
        _broadcaster.StartBroadcast(battleId, customFrequency);

        // Assert
        var config = _broadcaster.GetBattleConfig(battleId);
        Assert.NotNull(config);
        Assert.Equal(customFrequency, config.Frequency);
    }

    [Fact]
    public void StartBroadcast_WithFrequencyBelowMin_ShouldClampToMin()
    {
        // Arrange
        var battleId = "battle-123";
        var tooLowFrequency = 1; // 低于最小值2

        // Act
        _broadcaster.StartBroadcast(battleId, tooLowFrequency);

        // Assert
        var config = _broadcaster.GetBattleConfig(battleId);
        Assert.NotNull(config);
        Assert.Equal(_options.MinFrequency, config.Frequency);
    }

    [Fact]
    public void StartBroadcast_WithFrequencyAboveMax_ShouldClampToMax()
    {
        // Arrange
        var battleId = "battle-123";
        var tooHighFrequency = 15; // 高于最大值10

        // Act
        _broadcaster.StartBroadcast(battleId, tooHighFrequency);

        // Assert
        var config = _broadcaster.GetBattleConfig(battleId);
        Assert.NotNull(config);
        Assert.Equal(_options.MaxFrequency, config.Frequency);
    }

    [Fact]
    public void StartBroadcast_WithEmptyBattleId_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _broadcaster.StartBroadcast(""));
    }

    [Fact]
    public void StartBroadcast_WithNullBattleId_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _broadcaster.StartBroadcast(null!));
    }

    [Fact]
    public void StartBroadcast_MultipleBattles_ShouldAddAllToActiveBattles()
    {
        // Arrange
        var battleId1 = "battle-1";
        var battleId2 = "battle-2";
        var battleId3 = "battle-3";

        // Act
        _broadcaster.StartBroadcast(battleId1);
        _broadcaster.StartBroadcast(battleId2);
        _broadcaster.StartBroadcast(battleId3);

        // Assert
        Assert.Equal(3, _broadcaster.GetActiveBattleCount());
    }

    [Fact]
    public void StartBroadcast_ExceedMaxConcurrentBattles_ShouldNotAddNewBattle()
    {
        // Arrange
        var options = new CombatBroadcasterOptions
        {
            MaxConcurrentBattles = 2
        };
        var broadcaster = new CombatBroadcaster(
            _mockDispatcher.Object,
            _mockCoordinator.Object,
            _mockLogger.Object,
            Options.Create(options),
            Options.Create(_bufferOptions));

        // Act
        broadcaster.StartBroadcast("battle-1");
        broadcaster.StartBroadcast("battle-2");
        broadcaster.StartBroadcast("battle-3"); // 应该被拒绝

        // Assert
        Assert.Equal(2, broadcaster.GetActiveBattleCount());
    }

    #endregion

    #region StopBroadcast测试

    [Fact]
    public void StopBroadcast_WithActiveBattle_ShouldRemoveFromActiveBattles()
    {
        // Arrange
        var battleId = "battle-123";
        _broadcaster.StartBroadcast(battleId);

        // Act
        _broadcaster.StopBroadcast(battleId);

        // Assert
        Assert.Equal(0, _broadcaster.GetActiveBattleCount());
        Assert.Null(_broadcaster.GetBattleConfig(battleId));
    }

    [Fact]
    public void StopBroadcast_WithNonExistentBattle_ShouldNotThrow()
    {
        // Arrange
        var battleId = "non-existent";

        // Act & Assert
        var exception = Record.Exception(() => _broadcaster.StopBroadcast(battleId));
        Assert.Null(exception);
    }

    [Fact]
    public void StopBroadcast_WithEmptyBattleId_ShouldNotThrow()
    {
        // Act & Assert
        var exception = Record.Exception(() => _broadcaster.StopBroadcast(""));
        Assert.Null(exception);
    }

    #endregion

    #region SetFrequency测试

    [Fact]
    public void SetFrequency_WithActiveBattle_ShouldUpdateFrequency()
    {
        // Arrange
        var battleId = "battle-123";
        _broadcaster.StartBroadcast(battleId, 8);

        // Act
        _broadcaster.SetFrequency(battleId, 5);

        // Assert
        var config = _broadcaster.GetBattleConfig(battleId);
        Assert.NotNull(config);
        Assert.Equal(5, config.Frequency);
    }

    [Fact]
    public void SetFrequency_WithNonExistentBattle_ShouldNotThrow()
    {
        // Arrange
        var battleId = "non-existent";

        // Act & Assert
        var exception = Record.Exception(() => _broadcaster.SetFrequency(battleId, 5));
        Assert.Null(exception);
    }

    [Fact]
    public void SetFrequency_WithFrequencyBelowMin_ShouldClampToMin()
    {
        // Arrange
        var battleId = "battle-123";
        _broadcaster.StartBroadcast(battleId);

        // Act
        _broadcaster.SetFrequency(battleId, 1);

        // Assert
        var config = _broadcaster.GetBattleConfig(battleId);
        Assert.NotNull(config);
        Assert.Equal(_options.MinFrequency, config.Frequency);
    }

    [Fact]
    public void SetFrequency_WithFrequencyAboveMax_ShouldClampToMax()
    {
        // Arrange
        var battleId = "battle-123";
        _broadcaster.StartBroadcast(battleId);

        // Act
        _broadcaster.SetFrequency(battleId, 20);

        // Assert
        var config = _broadcaster.GetBattleConfig(battleId);
        Assert.NotNull(config);
        Assert.Equal(_options.MaxFrequency, config.Frequency);
    }

    #endregion

    #region BroadcastKeyEvent测试

    [Fact]
    public async Task BroadcastKeyEvent_WithValidEvent_ShouldCallDispatcher()
    {
        // Arrange
        var battleId = "battle-123";
        var keyEvent = new KeyEvent
        {
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            BattleId = battleId,
            Type = KeyEventType.SkillCast,
            Data = "{\"skillId\":\"fireball\"}"
        };

        // Act
        await _broadcaster.BroadcastKeyEvent(battleId, keyEvent);

        // Assert
        _mockDispatcher.Verify(
            d => d.SendToGroupAsync(
                $"battle:{battleId}",
                "KeyEvent",
                keyEvent,
                MessagePriority.Critical),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastKeyEvent_WithNullEvent_ShouldThrowArgumentNullException()
    {
        // Arrange
        var battleId = "battle-123";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _broadcaster.BroadcastKeyEvent(battleId, null!));
    }

    [Fact]
    public async Task BroadcastKeyEvent_WithEmptyBattleId_ShouldThrowArgumentException()
    {
        // Arrange
        var keyEvent = new KeyEvent
        {
            Type = KeyEventType.SkillCast
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _broadcaster.BroadcastKeyEvent("", keyEvent));
    }

    #endregion

    #region BroadcastSnapshot测试

    [Fact]
    public async Task BroadcastSnapshot_WithValidSnapshot_ShouldCallDispatcher()
    {
        // Arrange
        var battleId = "battle-123";
        var snapshot = new BattleSnapshot
        {
            Version = 100,
            ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            BattleId = battleId,
            State = new BattleState
            {
                Phase = BattlePhase.Active
            }
        };

        // Act
        await _broadcaster.BroadcastSnapshot(battleId, snapshot);

        // Assert
        _mockDispatcher.Verify(
            d => d.SendToGroupAsync(
                $"battle:{battleId}",
                "BattleSnapshot",
                snapshot,
                MessagePriority.High),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastSnapshot_WithNullSnapshot_ShouldThrowArgumentNullException()
    {
        // Arrange
        var battleId = "battle-123";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _broadcaster.BroadcastSnapshot(battleId, null!));
    }

    [Fact]
    public async Task BroadcastSnapshot_WithEmptyBattleId_ShouldThrowArgumentException()
    {
        // Arrange
        var snapshot = new BattleSnapshot();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _broadcaster.BroadcastSnapshot("", snapshot));
    }

    #endregion

    #region GetActiveBattleCount测试

    [Fact]
    public void GetActiveBattleCount_WithNoBattles_ShouldReturnZero()
    {
        // Act
        var count = _broadcaster.GetActiveBattleCount();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void GetActiveBattleCount_WithMultipleBattles_ShouldReturnCorrectCount()
    {
        // Arrange
        _broadcaster.StartBroadcast("battle-1");
        _broadcaster.StartBroadcast("battle-2");
        _broadcaster.StartBroadcast("battle-3");

        // Act
        var count = _broadcaster.GetActiveBattleCount();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void GetActiveBattleCount_AfterStoppingBattle_ShouldDecrement()
    {
        // Arrange
        _broadcaster.StartBroadcast("battle-1");
        _broadcaster.StartBroadcast("battle-2");
        _broadcaster.StopBroadcast("battle-1");

        // Act
        var count = _broadcaster.GetActiveBattleCount();

        // Assert
        Assert.Equal(1, count);
    }

    #endregion

    #region GetBattleConfig测试

    [Fact]
    public void GetBattleConfig_WithActiveBattle_ShouldReturnConfig()
    {
        // Arrange
        var battleId = "battle-123";
        _broadcaster.StartBroadcast(battleId, 6);

        // Act
        var config = _broadcaster.GetBattleConfig(battleId);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(6, config.Frequency);
        Assert.Equal(0, config.FrameCount);
    }

    [Fact]
    public void GetBattleConfig_WithNonExistentBattle_ShouldReturnNull()
    {
        // Act
        var config = _broadcaster.GetBattleConfig("non-existent");

        // Assert
        Assert.Null(config);
    }

    #endregion

    #region 配置验证测试

    [Fact]
    public void Options_Validate_WithValidConfig_ShouldNotThrow()
    {
        // Arrange
        var options = new CombatBroadcasterOptions
        {
            TickIntervalMs = 10,
            DefaultFrequency = 8,
            MinFrequency = 2,
            MaxFrequency = 10
        };

        // Act & Assert
        var exception = Record.Exception(() => options.Validate());
        Assert.Null(exception);
    }

    [Fact]
    public void Options_Validate_WithInvalidTickInterval_ShouldThrow()
    {
        // Arrange
        var options = new CombatBroadcasterOptions { TickIntervalMs = 0 };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Options_Validate_WithInvalidMinFrequency_ShouldThrow()
    {
        // Arrange
        var options = new CombatBroadcasterOptions { MinFrequency = 0 };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Options_Validate_WithMaxFrequencyLessThanMin_ShouldThrow()
    {
        // Arrange
        var options = new CombatBroadcasterOptions
        {
            MinFrequency = 5,
            MaxFrequency = 3
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Options_Validate_WithDefaultFrequencyOutOfRange_ShouldThrow()
    {
        // Arrange
        var options = new CombatBroadcasterOptions
        {
            MinFrequency = 2,
            MaxFrequency = 10,
            DefaultFrequency = 15
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    #endregion
}
