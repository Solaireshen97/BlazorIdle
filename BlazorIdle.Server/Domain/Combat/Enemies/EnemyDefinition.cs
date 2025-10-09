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
    
    // Phase 4: 怪物攻击属性
    public int BaseDamage { get; } = 0;                      // 基础攻击伤害
    public Damage.DamageType AttackDamageType { get; } = Damage.DamageType.Physical;  // 攻击伤害类型
    public double AttackIntervalSeconds { get; } = 2.0;      // 攻击间隔（秒）

    public EnemyDefinition(
        string id,
        string name,
        int level,
        int maxHp,
        double armor = 0,
        double magicResist = 0,
        double vulnPhys = 0,
        double vulnMagic = 0,
        double vulnTrue = 0,
        int baseDamage = 0,
        Damage.DamageType attackDamageType = Damage.DamageType.Physical,
        double attackIntervalSeconds = 2.0)
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
        BaseDamage = baseDamage;
        AttackDamageType = attackDamageType;
        AttackIntervalSeconds = attackIntervalSeconds;
    }
}