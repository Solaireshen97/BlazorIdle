namespace BlazorIdle.Server.Domain.Combat.Buffs;

public enum BuffStackPolicy
{
    Refresh = 0, // 重置持续时间，不改变层数
    Stack = 1, // 增加层数（不超过最大层），并重置持续时间
    Extend = 2  // 增加剩余时间（不超过总持续上限 = baseDuration * maxStacks），层数不变或到达上限不再增
}