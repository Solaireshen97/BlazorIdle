namespace BlazorIdle.Server.Domain.Combat.Resources;

public enum OverflowPolicy
{
    Clamp = 0,      // 到上限后截断
    Convert = 1,    // 按单位转化成 Tag（或未来 Buff / 另一资源）
    Ignore = 2      // 溢出完全忽略（不加不转）
}