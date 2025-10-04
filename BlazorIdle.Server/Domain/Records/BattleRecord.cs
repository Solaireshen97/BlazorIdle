namespace BlazorIdle.Server.Domain.Records;

public class BattleRecord
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public int TotalDamage { get; set; }
    public double DurationSeconds { get; set; }
    public double AttackIntervalSeconds { get; set; }
    public double SpecialIntervalSeconds { get; set; }

    public string Seed { get; set; } = "0";
    public long SeedIndexStart { get; set; }
    public long SeedIndexEnd { get; set; }

    // 敌人与击杀信息（新增）
    public string EnemyId { get; set; } = "dummy";
    public string EnemyName { get; set; } = "Training Dummy";
    public int EnemyLevel { get; set; }
    public int EnemyMaxHp { get; set; }
    public double EnemyArmor { get; set; }
    public double EnemyMagicResist { get; set; }
    public bool Killed { get; set; }
    public double? KillTimeSeconds { get; set; }
    public int OverkillDamage { get; set; }

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
    public string DamageBySourceJson { get; set; } = "{}";
    public string TagCountersJson { get; set; } = "{}";
    public string ResourceFlowJson { get; set; } = "{}";
    public string DamageByTypeJson { get; set; } = "{}"; // 新增
}