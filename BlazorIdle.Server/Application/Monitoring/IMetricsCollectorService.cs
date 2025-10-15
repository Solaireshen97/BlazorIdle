namespace BlazorIdle.Server.Application.Monitoring;

/// <summary>
/// Phase 6: 性能监控与指标收集服务接口
/// 定义指标收集的标准契约
/// </summary>
public interface IMetricsCollectorService
{
    // 战斗系统指标
    void RecordBattleDuration(Guid battleId, double durationSeconds, int eventCount);
    void RecordBattleEvents(Guid battleId, string eventType, int count);
    void RecordBattleDamage(Guid battleId, double totalDamage, double averageDps);

    // 经济系统指标
    void RecordGoldChange(Guid characterId, int delta, string source, int currentBalance);
    void RecordExperienceGain(Guid characterId, int delta, string source, int currentLevel);
    void RecordItemAcquisition(Guid characterId, Guid itemId, int quantity, string source);

    // 装备系统指标
    void RecordEquipmentOperation(string operationType, Guid characterId, Guid equipmentId, int cost);
    void RecordReforgeAttempt(Guid characterId, bool success, int tierBefore, int? tierAfter);

    // API性能指标
    void RecordApiDuration(string endpoint, string method, double durationMs, int statusCode);
    void RecordApiError(string endpoint, string errorType, int statusCode);

    // 资源使用指标
    void RecordDatabaseQuery(string queryType, double durationMs, int? rowCount = null);
    void RecordCacheAccess(string cacheKey, bool hit);

    // 离线系统指标
    void RecordOfflineFastForward(Guid characterId, double offlineDurationSeconds, double processingTimeMs, int eventsGenerated);

    // 活动计划指标
    void RecordActivityPlanExecution(Guid characterId, string planType, double durationSeconds, bool completed);
}
