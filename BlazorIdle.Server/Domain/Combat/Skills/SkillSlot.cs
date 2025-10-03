namespace BlazorIdle.Server.Domain.Combat.Skills;

public class SkillSlot
{
    public SkillRuntime Runtime { get; }

    public SkillSlot(SkillDefinition def)
    {
        Runtime = new SkillRuntime(def);
    }
}