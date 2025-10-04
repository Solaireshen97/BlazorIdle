using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Domain.Characters;

// 主属性 → 面板权重（Phase 1：线性映射；不提供急速）
public sealed class AttributeWeights
{
    public double StrToAP { get; init; } = 0.0;
    public double AgiToAP { get; init; } = 0.0;
    public double IntToSP { get; init; } = 0.0;

    // 暴击几率（绝对概率增量）
    public double StrToCrit { get; init; } = 0.0;
    public double AgiToCrit { get; init; } = 0.0;
    public double IntToCrit { get; init; } = 0.0;

    // 急速：保持为 0（由专门属性/BUFF 管理）
    public double StrToHaste { get; init; } = 0.0;
    public double AgiToHaste { get; init; } = 0.0;
    public double IntToHaste { get; init; } = 0.0;
}