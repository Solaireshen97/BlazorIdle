using System.Collections.Generic;

namespace BlazorWebGame.Domain.Combat
{
    public sealed class EventScheduler : IEventScheduler
    {
        // 最小堆：按 ExecuteAt 升序出队
        private readonly PriorityQueue<IGameEvent, double> _pq = new();

        public int Count => _pq.Count;

        public void Schedule(IGameEvent ev)
        {
            _pq.Enqueue(ev, ev.ExecuteAt);
        }

        public IGameEvent? PopNext()
        {
            if (_pq.Count == 0) return null;
            return _pq.Dequeue();
        }

        // 新增：查看队头但不出队
        public IGameEvent? PeekNext()
        {
            if (_pq.Count == 0) return null;
            _pq.TryPeek(out var next, out _);
            return next;
        }
    }
}