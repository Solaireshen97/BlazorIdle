using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Domain.Characters;

public static class AttributeWeightsRegistry
{
    // 说明：
    // - Crit 按“每点属性贡献的绝对概率”定义，例如 0.0008 = 每点 +0.08% 暴击
    // - Haste 暂不从主属性提供（保持数值稳定），需要时可放开
    public static AttributeWeights Resolve(Profession p) =>
        p switch
        {
            Profession.Warrior => new AttributeWeights
            {
                StrToAP = 1.0,
                AgiToAP = 0.3,
                IntToSP = 0.0,
                AgiToCrit = 0.0005, // 每点 AGI +0.05% 暴击
                StrToCrit = 0.0000,
                IntToCrit = 0.0000,
                AgiToHaste = 0.0,
                IntToHaste = 0.0
            },
            Profession.Ranger => new AttributeWeights
            {
                StrToAP = 0.3,
                AgiToAP = 1.0,
                IntToSP = 0.0,
                AgiToCrit = 0.0008, // 每点 AGI +0.08% 暴击
                StrToCrit = 0.0000,
                IntToCrit = 0.0000,
                AgiToHaste = 0.0,
                IntToHaste = 0.0
            },
            _ => new AttributeWeights
            {
                StrToAP = 0.6,
                AgiToAP = 0.6,
                IntToSP = 0.8,
                AgiToCrit = 0.0005,
                IntToCrit = 0.0003
            }
        };
}