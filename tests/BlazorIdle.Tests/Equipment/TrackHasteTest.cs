using BlazorIdle.Server.Domain.Combat;
using Xunit;
using Xunit.Abstractions;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 测试TrackState的急速机制
/// </summary>
public class TrackHasteTest
{
    private readonly ITestOutputHelper _output;

    public TrackHasteTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TrackState_ShouldApplyHasteFactor()
    {
        // Arrange
        double baseInterval = 2.0; // 2 seconds
        var track = new TrackState(TrackType.Attack, baseInterval, 0);
        
        _output.WriteLine($"Initial state:");
        _output.WriteLine($"  BaseInterval: {track.BaseInterval}");
        _output.WriteLine($"  HasteFactor: {track.HasteFactor}");
        _output.WriteLine($"  CurrentInterval: {track.CurrentInterval}");
        
        // Act - Apply 25% haste
        double hasteFactor = 1.25;
        track.SetHaste(hasteFactor);
        
        _output.WriteLine($"\nAfter applying {hasteFactor} haste factor:");
        _output.WriteLine($"  HasteFactor: {track.HasteFactor}");
        _output.WriteLine($"  CurrentInterval: {track.CurrentInterval}");
        _output.WriteLine($"  Expected: {baseInterval / hasteFactor}");
        
        // Assert
        Assert.Equal(hasteFactor, track.HasteFactor);
        Assert.Equal(baseInterval / hasteFactor, track.CurrentInterval);
        
        // Calculate how many attacks in 20 seconds
        double attacksIn20s = 20.0 / track.CurrentInterval;
        _output.WriteLine($"\nAttacks in 20 seconds: {attacksIn20s:F1}");
    }
}
