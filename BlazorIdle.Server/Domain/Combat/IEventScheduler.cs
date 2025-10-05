namespace BlazorWebGame.Domain.Combat
{
    public interface IEventScheduler
    {
        int Count { get; }

        // 入队一个事件（按 ExecuteAt 作为优先级）
        void Schedule(IGameEvent ev);

        // 取出并移除最近的事件；为空返回 null
        IGameEvent? PopNext();

        // 新增：查看最近的事件但不移除；为空返回 null
        IGameEvent? PeekNext();
    }
}