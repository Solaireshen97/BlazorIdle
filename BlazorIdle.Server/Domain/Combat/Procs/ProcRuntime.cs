namespace BlazorIdle.Server.Domain.Combat.Procs;

public sealed class ProcRuntime
{
    public ProcDefinition Definition { get; }
    public double IcdUntil { get; private set; } = 0;
    public double LastProcAt { get; private set; } = -1;

    public ProcRuntime(ProcDefinition def) => Definition = def;

    public bool InIcd(double now) => now < IcdUntil;

    public void MarkProc(double now)
    {
        LastProcAt = now;
        if (Definition.IcdSeconds > 0)
            IcdUntil = now + Definition.IcdSeconds;
    }
}