using System;
using System.Collections.Generic;

namespace BlazorIdle.Shared.Models
{
    /// <summary>
    /// 装备分解预览响应
    /// </summary>
    public record DisenchantPreviewResponse(
        Guid GearInstanceId,
        Dictionary<string, int> Materials
    );

    /// <summary>
    /// 装备分解响应
    /// </summary>
    public record DisenchantResponse(
        bool Success,
        string Message,
        Dictionary<string, int> Materials
    );

    /// <summary>
    /// 重铸预览响应
    /// </summary>
    public record ReforgePreviewResponse(
        bool CanReforge,
        string Message,
        int CurrentTier,
        int NextTier,
        Dictionary<string, int> Cost,
        Dictionary<string, double> CurrentStats,
        Dictionary<string, double> NextStats
    );

    /// <summary>
    /// 重铸响应
    /// </summary>
    public record ReforgeResponse(
        bool Success,
        string Message,
        ReforgedGearDto? Gear
    );

    /// <summary>
    /// 重铸后的装备信息
    /// </summary>
    public record ReforgedGearDto(
        Guid Id,
        string Name,
        int TierLevel,
        int QualityScore,
        Dictionary<string, double> RolledStats
    );
}
