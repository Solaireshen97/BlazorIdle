namespace BlazorIdle.Server.Domain.Economy;

public sealed class EnemyEconomy
{
    public string EnemyId { get; }
    public int Gold { get; }         // 每只怪基础金币
    public int Exp { get; }          // 每只怪基础经验
    public string? LootTableId { get; } // 掉落表（可空）

    public EnemyEconomy(string enemyId, int gold, int exp, string? lootTableId = null)
    {
        EnemyId = enemyId;
        Gold = gold;
        Exp = exp;
        LootTableId = lootTableId;
    }
}