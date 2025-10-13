using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Config;

/// <summary>
/// SignalR 配置启动验证器
/// 在应用启动时验证配置，失败则终止启动
/// </summary>
public sealed class SignalRStartupValidator : IHostedService
{
    private readonly IOptions<SignalROptions> _options;
    private readonly ILogger<SignalRStartupValidator> _logger;

    public SignalRStartupValidator(
        IOptions<SignalROptions> options,
        ILogger<SignalRStartupValidator> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始验证 SignalR 配置...");

        var validationResult = SignalROptionsValidator.Validate(_options.Value);

        if (!validationResult.IsValid)
        {
            var errorMessage = $"SignalR 配置验证失败: {validationResult.GetErrorMessage()}";
            _logger.LogCritical("{ErrorMessage}", errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogInformation("SignalR 配置验证通过");
        LogConfigurationSummary();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 记录配置摘要
    /// </summary>
    private void LogConfigurationSummary()
    {
        var options = _options.Value;
        
        _logger.LogInformation(
            "SignalR 配置摘要: " +
            "启用={Enabled}, Hub端点={HubEndpoint}, " +
            "最大重连次数={MaxReconnect}, 节流={Throttling}",
            options.EnableSignalR,
            options.HubEndpoint,
            options.MaxReconnectAttempts,
            options.Performance.EnableThrottling
        );

        // 记录启用的通知类型
        var enabledTypes = new List<string>();
        if (options.Notification.EnablePlayerDeathNotification)
            enabledTypes.Add("PlayerDeath");
        if (options.Notification.EnablePlayerReviveNotification)
            enabledTypes.Add("PlayerRevive");
        if (options.Notification.EnableEnemyKilledNotification)
            enabledTypes.Add("EnemyKilled");
        if (options.Notification.EnableTargetSwitchedNotification)
            enabledTypes.Add("TargetSwitched");
        if (options.Notification.EnableWaveSpawnNotification)
            enabledTypes.Add("WaveSpawn");
        if (options.Notification.EnableSkillCastNotification)
            enabledTypes.Add("SkillCast");
        if (options.Notification.EnableBuffChangeNotification)
            enabledTypes.Add("BuffChange");

        _logger.LogInformation(
            "已启用 {Count} 个通知类型: {Types}",
            enabledTypes.Count,
            string.Join(", ", enabledTypes)
        );

        // 记录性能配置
        if (options.Performance.EnableThrottling)
        {
            _logger.LogInformation(
                "节流配置: 窗口={WindowMs}ms",
                options.Performance.ThrottleWindowMs
            );
        }

        if (options.Performance.EnableBatching)
        {
            _logger.LogInformation(
                "批处理配置: 延迟={DelayMs}ms",
                options.Performance.BatchDelayMs
            );
        }
    }
}
