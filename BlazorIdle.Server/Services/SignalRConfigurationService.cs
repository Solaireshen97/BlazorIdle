using BlazorIdle.Server.Config;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Services;

/// <summary>
/// SignalR 配置服务
/// 提供配置访问、验证和监控功能
/// </summary>
public sealed class SignalRConfigurationService
{
    private readonly SignalROptions _options;
    private readonly ILogger<SignalRConfigurationService> _logger;
    private DateTime _lastConfigLoadTime;
    private int _configAccessCount;
    private readonly object _lock = new();

    public SignalRConfigurationService(
        IOptions<SignalROptions> options,
        ILogger<SignalRConfigurationService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _lastConfigLoadTime = DateTime.UtcNow;
        _configAccessCount = 0;
    }

    /// <summary>
    /// 获取配置选项
    /// </summary>
    public SignalROptions Options
    {
        get
        {
            Interlocked.Increment(ref _configAccessCount);
            return _options;
        }
    }

    /// <summary>
    /// 验证当前配置
    /// </summary>
    /// <returns>验证结果</returns>
    public SignalROptionsValidator.ValidationResult ValidateConfiguration()
    {
        return SignalROptionsValidator.Validate(_options);
    }

    /// <summary>
    /// 检查事件类型是否启用
    /// </summary>
    public bool IsEventTypeEnabled(string eventType)
    {
        return eventType switch
        {
            "PlayerDeath" => _options.Notification.EnablePlayerDeathNotification,
            "PlayerRevive" => _options.Notification.EnablePlayerReviveNotification,
            "EnemyKilled" => _options.Notification.EnableEnemyKilledNotification,
            "TargetSwitched" => _options.Notification.EnableTargetSwitchedNotification,
            "WaveSpawn" => _options.Notification.EnableWaveSpawnNotification,
            "SkillCast" => _options.Notification.EnableSkillCastNotification,
            "BuffChange" => _options.Notification.EnableBuffChangeNotification,
            _ => true // 默认启用未知类型
        };
    }

    /// <summary>
    /// 获取配置统计信息
    /// </summary>
    public ConfigurationStats GetStatistics()
    {
        lock (_lock)
        {
            return new ConfigurationStats
            {
                LastLoadTime = _lastConfigLoadTime,
                AccessCount = _configAccessCount,
                IsSignalREnabled = _options.EnableSignalR,
                IsThrottlingEnabled = _options.Performance.EnableThrottling,
                EnabledNotificationTypes = GetEnabledNotificationTypes()
            };
        }
    }

    /// <summary>
    /// 获取已启用的通知类型列表
    /// </summary>
    private List<string> GetEnabledNotificationTypes()
    {
        var types = new List<string>();
        
        if (_options.Notification.EnablePlayerDeathNotification)
            types.Add("PlayerDeath");
        if (_options.Notification.EnablePlayerReviveNotification)
            types.Add("PlayerRevive");
        if (_options.Notification.EnableEnemyKilledNotification)
            types.Add("EnemyKilled");
        if (_options.Notification.EnableTargetSwitchedNotification)
            types.Add("TargetSwitched");
        if (_options.Notification.EnableWaveSpawnNotification)
            types.Add("WaveSpawn");
        if (_options.Notification.EnableSkillCastNotification)
            types.Add("SkillCast");
        if (_options.Notification.EnableBuffChangeNotification)
            types.Add("BuffChange");
            
        return types;
    }

    /// <summary>
    /// 记录配置使用情况
    /// </summary>
    public void LogConfigurationUsage()
    {
        var stats = GetStatistics();
        _logger.LogInformation(
            "SignalR 配置统计: SignalR启用={SignalREnabled}, 节流启用={ThrottlingEnabled}, " +
            "已启用通知类型={NotificationTypes}, 配置访问次数={AccessCount}",
            stats.IsSignalREnabled,
            stats.IsThrottlingEnabled,
            string.Join(", ", stats.EnabledNotificationTypes),
            stats.AccessCount
        );
    }

    /// <summary>
    /// 配置统计信息
    /// </summary>
    public sealed class ConfigurationStats
    {
        public DateTime LastLoadTime { get; set; }
        public int AccessCount { get; set; }
        public bool IsSignalREnabled { get; set; }
        public bool IsThrottlingEnabled { get; set; }
        public List<string> EnabledNotificationTypes { get; set; } = new();
    }
}
