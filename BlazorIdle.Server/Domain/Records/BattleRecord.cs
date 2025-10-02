namespace BlazorIdle.Server.Domain.Records;

public class BattleRecord
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public int TotalDamage { get; set; }
    public double DurationSeconds { get; set; }
    public ICollection<BattleSegmentRecord> Segments { get; set; } = new List<BattleSegmentRecord>();
}

public class BattleSegmentRecord
{
    public Guid Id { get; set; }
    public Guid BattleId { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public int EventCount { get; set; }
    public int TotalDamage { get; set; }
    public string DamageBySourceJson { get; set; } = "{}"; // 简单 JSON 字符串存来源分布
}