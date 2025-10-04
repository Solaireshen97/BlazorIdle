namespace BlazorIdle.Server.Domain.Combat.Enemies;

public class EnemyDefinition
{
    public string Id { get; }
    public string Name { get; }
    public int Level { get; }
    public int MaxHp { get; }
    public double Armor { get; }         // 物理减伤使用
    public double MagicResist { get; }   // 0..1 表示百分比（例如 0.2 = 20% 减伤）
    public double VulnerabilityPhysical { get; } = 0.0; // 额外易伤（+0.1 = +10% 伤害）
    public double VulnerabilityMagic { get; } = 0.0;
    public double VulnerabilityTrue { get; } = 0.0;

    public EnemyDefinition(
        string id,
        string name,
        int level,
        int maxHp,
        double armor = 0,
        double magicResist = 0,
        double vulnPhys = 0,
        double vulnMagic = 0,
        double vulnTrue = 0)
    {
        Id = id;
        Name = name;
        Level = level;
        MaxHp = maxHp;
        Armor = armor;
        MagicResist = magicResist;
        VulnerabilityPhysical = vulnPhys;
        VulnerabilityMagic = vulnMagic;
        VulnerabilityTrue = vulnTrue;
    }
}