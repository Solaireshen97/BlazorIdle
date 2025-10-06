using Microsoft.Extensions.Hosting;

namespace BlazorIdle.Server.Application.Activities;

/// <summary>
/// 活动后台服务：周期性推进所有活动执行
/// </summary>
public sealed class ActivityHostedService : BackgroundService
{
    private readonly ActivityCoordinator _coordinator;
    private readonly ILogger<ActivityHostedService> _logger;
    private readonly TimeSpan _advanceInterval;
    private readonly TimeSpan _pruneInterval;
    
    private DateTime _lastPruneTime = DateTime.UtcNow;
    
    public ActivityHostedService(
        ActivityCoordinator coordinator,
        ILogger<ActivityHostedService> logger,
        IConfiguration config)
    {
        _coordinator = coordinator;
        _logger = logger;
        
        // 默认每1秒推进一次
        _advanceInterval = TimeSpan.FromSeconds(config.GetValue<double>("Activity:AdvanceIntervalSeconds", 1.0));
        
        // 默认每10分钟清理一次已完成的计划
        _pruneInterval = TimeSpan.FromMinutes(config.GetValue<double>("Activity:PruneIntervalMinutes", 10.0));
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ActivityHostedService started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 推进所有活动
                await _coordinator.AdvanceAllAsync(stoppingToken);
                
                // 定期清理已完成的计划
                if (DateTime.UtcNow - _lastPruneTime > _pruneInterval)
                {
                    var removed = _coordinator.PruneCompletedPlans(TimeSpan.FromHours(1));
                    if (removed > 0)
                    {
                        _logger.LogInformation("Pruned {Count} completed activity plans", removed);
                    }
                    _lastPruneTime = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ActivityHostedService");
            }
            
            await Task.Delay(_advanceInterval, stoppingToken);
        }
        
        _logger.LogInformation("ActivityHostedService stopped");
    }
}
