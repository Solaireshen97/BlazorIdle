using System.Net.Http.Json;
using BlazorIdle.Models;

namespace BlazorIdle.Services;

/// <summary>
/// 进度条配置服务，负责加载和管理前端进度条配置
/// </summary>
public class ProgressBarConfigService
{
    private readonly HttpClient _httpClient;
    private ProgressBarConfig? _config;
    private bool _isLoaded;

    public ProgressBarConfigService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// 获取配置，如果未加载则先加载
    /// </summary>
    public async Task<ProgressBarConfig> GetConfigAsync()
    {
        if (!_isLoaded)
        {
            await LoadConfigAsync();
        }
        return _config ?? CreateDefaultConfig();
    }

    /// <summary>
    /// 从服务器加载配置文件
    /// </summary>
    private async Task LoadConfigAsync()
    {
        try
        {
            _config = await _httpClient.GetFromJsonAsync<ProgressBarConfig>("config/progress-bar-config.json");
            _isLoaded = true;
        }
        catch (Exception)
        {
            // 加载失败时使用默认配置
            _config = CreateDefaultConfig();
            _isLoaded = true;
        }
    }

    /// <summary>
    /// 创建默认配置
    /// </summary>
    private static ProgressBarConfig CreateDefaultConfig()
    {
        return new ProgressBarConfig
        {
            ProgressBar = new ProgressBarSettings
            {
                EnableLoopingProgress = true,
                AnimationIntervalMs = 100,
                MinIntervalForLooping = 0.1,
                MaxIntervalForLooping = 100.0
            },
            JITPolling = new JITPollingSettings
            {
                EnableJITPolling = true,
                TriggerWindowMs = 150,
                MinPredictionTimeMs = 100,
                MaxJITAttemptsPerCycle = 1,
                AdaptivePollingEnabled = true,
                MinPollingIntervalMs = 200,
                MaxPollingIntervalMs = 2000,
                HealthCriticalThreshold = 0.3,
                HealthLowThreshold = 0.5,
                CriticalHealthPollingMs = 500,
                LowHealthPollingMs = 1000,
                NormalPollingMs = 2000
            },
            HPAnimation = new HPAnimationSettings
            {
                TransitionDurationMs = 120,
                TransitionTimingFunction = "linear",
                EnableSmoothTransition = true,
                PlayerHPTransitionMs = 120,
                EnemyHPTransitionMs = 120
            },
            SignalRIncrementalUpdate = new SignalRIncrementalUpdateSettings
            {
                EnableIncrementalUpdate = true,
                EnableAttackTickUpdate = true,
                EnableSkillCastUpdate = true,
                EnableDamageAppliedUpdate = false,
                ClientPredictionEnabled = true,
                MaxPredictionAheadMs = 500,
                SyncThresholdMs = 100,
                ResetProgressOnMismatch = true
            },
            Debug = new DebugSettings
            {
                LogProgressCalculations = false,
                LogJITPollingEvents = false,
                ShowProgressDebugInfo = false,
                LogSignalREvents = false,
                LogIncrementalUpdates = false
            }
        };
    }

    /// <summary>
    /// 重新加载配置
    /// </summary>
    public async Task ReloadConfigAsync()
    {
        _isLoaded = false;
        await LoadConfigAsync();
    }
}
