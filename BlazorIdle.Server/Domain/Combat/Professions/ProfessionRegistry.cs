using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Domain.Combat.Professions;

public static class ProfessionRegistry
{
    public static IProfessionModule Resolve(Profession profession) =>
        profession switch
        {
            Profession.Warrior => new WarriorProfession(),
            Profession.Ranger => new RangerProfession(),
            _ => new WarriorProfession()
        };
}