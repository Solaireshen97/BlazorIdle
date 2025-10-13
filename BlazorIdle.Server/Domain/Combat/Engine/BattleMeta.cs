using BlazorIdle.Server.Application.Abstractions;

namespace BlazorIdle.Server.Domain.Combat.Engine;

public sealed class BattleMeta
{
    // 建议传入小写 mode 标签：duration | continuous | dungeonsingle | dungeonloop（或 dungeon）
    public string ModeTag { get; init; } = "duration";
    public string EnemyId { get; init; } = "dummy";
    public int EnemyCount { get; init; } = 1;
    public string? DungeonId { get; init; }

    // 预留：额外自定义标签（key 为完整 tag，如 "ctx.foo.bar"）
    public Dictionary<string, int>? ExtraTags { get; init; }
    
    // SignalR 实时通知服务（Phase 2）
    public IBattleNotificationService? NotificationService { get; init; }
}