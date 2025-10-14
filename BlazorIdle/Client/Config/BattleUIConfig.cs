using System.Text.Json.Serialization;

namespace BlazorIdle.Client.Config;

/// <summary>
/// Battle UI configuration loaded from battle-ui-config.json
/// Controls client-side battle UI behavior including progress bars, polling, and animations
/// </summary>
public class BattleUIConfig
{
    [JsonPropertyName("progressBar")]
    public ProgressBarConfig ProgressBar { get; set; } = new();
    
    [JsonPropertyName("polling")]
    public PollingConfig Polling { get; set; } = new();
    
    [JsonPropertyName("animation")]
    public AnimationConfig Animation { get; set; } = new();
    
    [JsonPropertyName("debug")]
    public DebugConfig Debug { get; set; } = new();
}

public class ProgressBarConfig
{
    [JsonPropertyName("enableCycling")]
    public bool EnableCycling { get; set; } = true;
    
    [JsonPropertyName("minIntervalSeconds")]
    public double MinIntervalSeconds { get; set; } = 0.1;
    
    [JsonPropertyName("maxIntervalSeconds")]
    public double MaxIntervalSeconds { get; set; } = 100.0;
}

public class PollingConfig
{
    [JsonPropertyName("step")]
    public PollingModeConfig Step { get; set; } = new();
    
    [JsonPropertyName("plan")]
    public PollingModeConfig Plan { get; set; } = new();
    
    [JsonPropertyName("adaptive")]
    public AdaptivePollingConfig Adaptive { get; set; } = new();
}

public class PollingModeConfig
{
    [JsonPropertyName("normalIntervalMs")]
    public int NormalIntervalMs { get; set; } = 500;
    
    [JsonPropertyName("slowIntervalMs")]
    public int SlowIntervalMs { get; set; } = 2000;
    
    [JsonPropertyName("fastIntervalMs")]
    public int FastIntervalMs { get; set; } = 200;
    
    [JsonPropertyName("minIntervalMs")]
    public int MinIntervalMs { get; set; } = 100;
    
    [JsonPropertyName("maxIntervalMs")]
    public int MaxIntervalMs { get; set; } = 5000;
}

public class AdaptivePollingConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
    
    [JsonPropertyName("triggerLeadTimeMs")]
    public int TriggerLeadTimeMs { get; set; } = 150;
    
    [JsonPropertyName("minLeadTimeMs")]
    public int MinLeadTimeMs { get; set; } = 50;
    
    [JsonPropertyName("maxLeadTimeMs")]
    public int MaxLeadTimeMs { get; set; } = 500;
    
    [JsonPropertyName("cooldownAfterTriggerMs")]
    public int CooldownAfterTriggerMs { get; set; } = 300;
    
    [JsonPropertyName("maxJitPollsPerSecond")]
    public int MaxJitPollsPerSecond { get; set; } = 5;
}

public class AnimationConfig
{
    [JsonPropertyName("progressBarUpdateIntervalMs")]
    public int ProgressBarUpdateIntervalMs { get; set; } = 100;
    
    [JsonPropertyName("hpBarTransitionMs")]
    public int HpBarTransitionMs { get; set; } = 120;
    
    [JsonPropertyName("attackProgressTransitionMs")]
    public int AttackProgressTransitionMs { get; set; } = 100;
}

public class DebugConfig
{
    [JsonPropertyName("enableLogging")]
    public bool EnableLogging { get; set; } = false;
    
    [JsonPropertyName("logProgressCalculations")]
    public bool LogProgressCalculations { get; set; } = false;
    
    [JsonPropertyName("logPollingEvents")]
    public bool LogPollingEvents { get; set; } = false;
    
    [JsonPropertyName("logJitPolls")]
    public bool LogJitPolls { get; set; } = false;
}
