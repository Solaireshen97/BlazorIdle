namespace BlazorIdle.Server.Domain.Combat.Skills;

public class SkillRuntime
{
    public SkillDefinition Definition { get; }
    public double NextAvailableTime { get; private set; } = 0;
    public int CastCount { get; private set; }

    public SkillRuntime(SkillDefinition def)
    {
        Definition = def;
    }

    public bool IsReady(double now) => now >= NextAvailableTime;

    public void MarkCast(double now)
    {
        CastCount++;
        NextAvailableTime = now + Definition.CooldownSeconds;
    }
}