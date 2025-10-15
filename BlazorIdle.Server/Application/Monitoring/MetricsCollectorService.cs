using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Application.Monitoring;

/// <summary>
/// Phase 6: 性能监控与指标收集服务
/// 用于收集和记录业务指标与技术指标，提升系统可观测性
/// </summary>
/// <remarks>
/// 设计原则：
/// 1. 可选注入 - 不影响核心业务逻辑
/// 2. 结构化日志 - 便于分析和查询
/// 3. 低开销 - 避免影响性能
/// 4. 可扩展 - 支持新增指标类型
/// 
/// 使用场景：
/// - 战斗系统性能监控
/// - 经济系统流水统计
/// - API响应时间追踪
/// - 资源使用监控
/// </remarks>
public class MetricsCollectorService : IMetricsCollectorService
{
    private readonly ILogger<MetricsCollectorService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器，用于输出指标数据</param>
    public MetricsCollectorService(ILogger<MetricsCollectorService> logger)
    {
        _logger = logger;
    }

    #region 战斗系统指标

    /// <summary>
    /// 记录战斗持续时间
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="durationSeconds">战斗持续时间（秒）</param>
    /// <param name="eventCount">事件总数</param>
    public void RecordBattleDuration(Guid battleId, double durationSeconds, int eventCount)
    {
        _logger.LogInformation(
            "[Metrics] 战斗时长统计: BattleId={BattleId}, Duration={DurationSeconds}s, EventCount={EventCount}, EventsPerSecond={EventsPerSecond:F2}",
            battleId,
            durationSeconds,
            eventCount,
            eventCount / Math.Max(durationSeconds, 0.001)
        );
    }

    /// <summary>
    /// 记录战斗事件统计
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="count">事件数量</param>
    public void RecordBattleEvents(Guid battleId, string eventType, int count)
    {
        _logger.LogDebug(
            "[Metrics] 战斗事件统计: BattleId={BattleId}, EventType={EventType}, Count={Count}",
            battleId,
            eventType,
            count
        );
    }

    /// <summary>
    /// 记录战斗伤害统计
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="totalDamage">总伤害</param>
    /// <param name="averageDps">平均DPS</param>
    public void RecordBattleDamage(Guid battleId, double totalDamage, double averageDps)
    {
        _logger.LogInformation(
            "[Metrics] 战斗伤害统计: BattleId={BattleId}, TotalDamage={TotalDamage:F0}, AverageDPS={AverageDps:F2}",
            battleId,
            totalDamage,
            averageDps
        );
    }

    #endregion

    #region 经济系统指标

    /// <summary>
    /// 记录金币变更
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="delta">金币变化量（正数为增加，负数为减少）</param>
    /// <param name="source">来源（如 "战斗奖励"、"装备分解"、"商店购买"）</param>
    /// <param name="currentBalance">当前余额</param>
    public void RecordGoldChange(Guid characterId, int delta, string source, int currentBalance)
    {
        var changeType = delta > 0 ? "收入" : "支出";
        _logger.LogInformation(
            "[Metrics] 金币变更: CharacterId={CharacterId}, {ChangeType}={Amount}, Source={Source}, Balance={Balance}",
            characterId,
            changeType,
            Math.Abs(delta),
            source,
            currentBalance
        );
    }

    /// <summary>
    /// 记录经验变更
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="delta">经验变化量</param>
    /// <param name="source">来源</param>
    /// <param name="currentLevel">当前等级</param>
    public void RecordExperienceGain(Guid characterId, int delta, string source, int currentLevel)
    {
        _logger.LogInformation(
            "[Metrics] 经验获得: CharacterId={CharacterId}, Experience={Experience}, Source={Source}, Level={Level}",
            characterId,
            delta,
            source,
            currentLevel
        );
    }

    /// <summary>
    /// 记录物品获得
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="itemId">物品ID</param>
    /// <param name="quantity">数量</param>
    /// <param name="source">来源</param>
    public void RecordItemAcquisition(Guid characterId, Guid itemId, int quantity, string source)
    {
        _logger.LogInformation(
            "[Metrics] 物品获得: CharacterId={CharacterId}, ItemId={ItemId}, Quantity={Quantity}, Source={Source}",
            characterId,
            itemId,
            quantity,
            source
        );
    }

    #endregion

    #region 装备系统指标

    /// <summary>
    /// 记录装备操作频率
    /// </summary>
    /// <param name="operationType">操作类型（如 "分解"、"重铸"、"词条重置"）</param>
    /// <param name="characterId">角色ID</param>
    /// <param name="equipmentId">装备ID</param>
    /// <param name="cost">消耗（金币或材料）</param>
    public void RecordEquipmentOperation(string operationType, Guid characterId, Guid equipmentId, int cost)
    {
        _logger.LogInformation(
            "[Metrics] 装备操作: Operation={Operation}, CharacterId={CharacterId}, EquipmentId={EquipmentId}, Cost={Cost}",
            operationType,
            characterId,
            equipmentId,
            cost
        );
    }

    /// <summary>
    /// 记录重铸成功率
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="success">是否成功</param>
    /// <param name="tierBefore">重铸前品级</param>
    /// <param name="tierAfter">重铸后品级</param>
    public void RecordReforgeAttempt(Guid characterId, bool success, int tierBefore, int? tierAfter)
    {
        _logger.LogInformation(
            "[Metrics] 装备重铸: CharacterId={CharacterId}, Success={Success}, TierBefore={TierBefore}, TierAfter={TierAfter}",
            characterId,
            success,
            tierBefore,
            tierAfter ?? tierBefore
        );
    }

    #endregion

    #region API性能指标

    /// <summary>
    /// 记录API请求响应时间
    /// </summary>
    /// <param name="endpoint">API端点</param>
    /// <param name="method">HTTP方法</param>
    /// <param name="durationMs">响应时间（毫秒）</param>
    /// <param name="statusCode">HTTP状态码</param>
    public void RecordApiDuration(string endpoint, string method, double durationMs, int statusCode)
    {
        var level = durationMs > 1000 ? LogLevel.Warning : LogLevel.Debug;
        _logger.Log(
            level,
            "[Metrics] API性能: Endpoint={Endpoint}, Method={Method}, Duration={DurationMs}ms, Status={StatusCode}",
            endpoint,
            method,
            durationMs,
            statusCode
        );
    }

    /// <summary>
    /// 记录API错误率
    /// </summary>
    /// <param name="endpoint">API端点</param>
    /// <param name="errorType">错误类型</param>
    /// <param name="statusCode">HTTP状态码</param>
    public void RecordApiError(string endpoint, string errorType, int statusCode)
    {
        _logger.LogWarning(
            "[Metrics] API错误: Endpoint={Endpoint}, ErrorType={ErrorType}, Status={StatusCode}",
            endpoint,
            errorType,
            statusCode
        );
    }

    #endregion

    #region 资源使用指标

    /// <summary>
    /// 记录数据库查询性能
    /// </summary>
    /// <param name="queryType">查询类型</param>
    /// <param name="durationMs">查询耗时（毫秒）</param>
    /// <param name="rowCount">返回行数（可选）</param>
    public void RecordDatabaseQuery(string queryType, double durationMs, int? rowCount = null)
    {
        var level = durationMs > 500 ? LogLevel.Warning : LogLevel.Debug;
        _logger.Log(
            level,
            "[Metrics] 数据库查询: QueryType={QueryType}, Duration={DurationMs}ms, RowCount={RowCount}",
            queryType,
            durationMs,
            rowCount ?? 0
        );
    }

    /// <summary>
    /// 记录缓存命中率
    /// </summary>
    /// <param name="cacheKey">缓存键类型</param>
    /// <param name="hit">是否命中</param>
    public void RecordCacheAccess(string cacheKey, bool hit)
    {
        _logger.LogDebug(
            "[Metrics] 缓存访问: CacheKey={CacheKey}, Hit={Hit}",
            cacheKey,
            hit
        );
    }

    #endregion

    #region 离线系统指标

    /// <summary>
    /// 记录离线快进处理时间
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="offlineDurationSeconds">离线时长（秒）</param>
    /// <param name="processingTimeMs">处理耗时（毫秒）</param>
    /// <param name="eventsGenerated">生成的事件数</param>
    public void RecordOfflineFastForward(Guid characterId, double offlineDurationSeconds, double processingTimeMs, int eventsGenerated)
    {
        _logger.LogInformation(
            "[Metrics] 离线快进: CharacterId={CharacterId}, OfflineDuration={OfflineSeconds}s, ProcessingTime={ProcessingMs}ms, Events={Events}",
            characterId,
            offlineDurationSeconds,
            processingTimeMs,
            eventsGenerated
        );
    }

    #endregion

    #region 活动计划指标

    /// <summary>
    /// 记录活动计划执行统计
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="planType">计划类型</param>
    /// <param name="durationSeconds">执行时长（秒）</param>
    /// <param name="completed">是否完成</param>
    public void RecordActivityPlanExecution(Guid characterId, string planType, double durationSeconds, bool completed)
    {
        _logger.LogInformation(
            "[Metrics] 活动计划执行: CharacterId={CharacterId}, PlanType={PlanType}, Duration={DurationSeconds}s, Completed={Completed}",
            characterId,
            planType,
            durationSeconds,
            completed
        );
    }

    #endregion
}
