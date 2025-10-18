using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using System;

namespace BlazorIdle.Server.Domain.Records;

public class RunningBattleSnapshotRecord : IEntity
{
    public Guid Id { get; set; }                    // 行主键
    public Guid StepBattleId { get; set; }          // 正在运行的内存战斗 Id（RunningBattle.Id）
    public Guid CharacterId { get; set; }
    public int Profession { get; set; }             // enum(int)
    public string EnemyId { get; set; } = "dummy";
    public int EnemyCount { get; set; }
    public string Seed { get; set; } = "0";

    public double TargetSeconds { get; set; }
    public double SimulatedSeconds { get; set; }

    public DateTime UpdatedAtUtc { get; set; }      // 最近一次更新
    public string SnapshotJson { get; set; } = "{}";// 含 Segments 聚合快照（JSON，便于复盘）
}