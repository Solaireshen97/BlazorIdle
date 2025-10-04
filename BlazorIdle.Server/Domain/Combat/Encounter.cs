using BlazorIdle.Server.Domain.Combat.Enemies;

namespace BlazorIdle.Server.Domain.Combat;

public class Encounter
{
    public EnemyDefinition Enemy { get; }
    public int CurrentHp { get; private set; }
    public bool IsDead => CurrentHp <= 0;
    public double? KillTime { get; private set; }
    public int Overkill { get; private set; }

    public Encounter(EnemyDefinition enemy)
    {
        Enemy = enemy;
        CurrentHp = enemy.MaxHp;
    }

    public int ApplyDamage(int amount, double now)
    {
        if (IsDead) return 0;
        var before = CurrentHp;
        CurrentHp -= amount;
        int applied = amount;
        if (CurrentHp <= 0)
        {
            Overkill = -CurrentHp;
            CurrentHp = 0;
            if (KillTime is null) KillTime = now;
        }
        return applied;
    }
}