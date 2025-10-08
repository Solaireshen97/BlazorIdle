namespace BlazorIdle.Server.Application.Battles.Offline;

/// <summary>
/// 离线系统配置选项
/// </summary>
public class OfflineOptions
{
    /// <summary>
    /// 玩家被认为离线的心跳超时阈值（秒），默认60秒
    /// </summary>
    public double OfflineThresholdSeconds { get; set; } = 60;

    /// <summary>
    /// 离线收益计算的最大时长上限（秒），默认12小时
    /// </summary>
    public double MaxOfflineSeconds { get; set; } = 43200;

    /// <summary>
    /// 是否启用自动离线结算（心跳更新时自动结算并发放）
    /// </summary>
    public bool EnableAutoSettlement { get; set; } = true;
}
