using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Domain.Characters;

// 权重定义：主属性如何转化为面板（Phase 1：线性映射）
// 建议数值：便于直观感知且不破坏现有平衡（你可根据需要微调）
public sealed class AttributeWeights
{
    // 派生
    public double StrToAP { get; init; } = 0.0;
    public double AgiToAP { get; init; } = 0.0;
    public double IntToSP { get; init; } = 0.0;

    // 暴击几率（绝对值 0..1 的增量）
    public double StrToCrit { get; init; } = 0.0;
    public double AgiToCrit { get; init; } = 0.0;
    public double IntToCrit { get; init; } = 0.0;

    // 急速（Phase 1 默认不开放，可按需开启）
    public double AgiToHaste { get; init; } = 0.0;
    public double IntToHaste { get; init; } = 0.0;

    public static AttributeWeights ForProfession(Profession p) =>
        AttributeWeightsRegistry.Resolve(p);
}