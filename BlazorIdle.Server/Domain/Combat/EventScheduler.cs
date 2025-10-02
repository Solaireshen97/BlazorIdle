using System.Collections.Generic;

namespace BlazorWebGame.Domain.Combat;

public interface IEventScheduler
{
    void Schedule(IGameEvent ev);
    IGameEvent? PopNext();
    int Count { get; }
}

public class EventScheduler : IEventScheduler
{
    private readonly PriorityQueue<IGameEvent, double> _pq = new();
    public void Schedule(IGameEvent ev) => _pq.Enqueue(ev, ev.ExecuteAt);
    public IGameEvent? PopNext() => _pq.TryDequeue(out var ev, out _) ? ev : null;
    public int Count => _pq.Count;
}