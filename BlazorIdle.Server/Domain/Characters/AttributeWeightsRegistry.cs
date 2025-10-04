using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Domain.Characters;

public static class AttributeWeightsRegistry
{
    public static AttributeWeights Resolve(Profession p) =>
        p switch
        {
            Profession.Warrior => new AttributeWeights
            {
                StrToAP = 1.0,
                AgiToAP = 0.3,
                IntToSP = 0.0,
                AgiToCrit = 0.0005 // +0.05% crit / AGI
            },
            Profession.Ranger => new AttributeWeights
            {
                StrToAP = 0.3,
                AgiToAP = 1.0,
                IntToSP = 0.0,
                AgiToCrit = 0.0008 // +0.08% crit / AGI
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