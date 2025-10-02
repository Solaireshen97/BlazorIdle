namespace BlazorWebGame.Domain.Combat;

public interface IGameEvent
{
    double ExecuteAt { get; }
    void Execute(BattleContext context);
    string EventType { get; }
}

public record AttackTickEvent(double ExecuteAt) : IGameEvent
{
    public string EventType => "AttackTick";
    public void Execute(BattleContext context)
    {
        // 简单伤害计算：固定 10，后续接入职业/装备
        const int dmg = 10;
        context.SegmentCollector.OnDamage("basic_attack", dmg);
        // 重新调度下一次
        var interval = context.Battle.AttackIntervalSeconds;
        context.Scheduler.Schedule(new AttackTickEvent(ExecuteAt + interval));
    }
}