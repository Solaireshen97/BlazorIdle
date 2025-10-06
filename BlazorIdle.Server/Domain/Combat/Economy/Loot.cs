namespace BlazorIdle.Server.Domain.Economy;

public sealed class LootEntry
{
    // 概率（0..1），一次 roll 是否掉
    public double DropChance { get; init; } = 0.0;
    public string ItemId { get; init; } = "";
    public int QuantityMin { get; init; } = 1;
    public int QuantityMax { get; init; } = 1;
}

public sealed class LootTable
{
    public string Id { get; }
    public IReadOnlyList<LootEntry> Entries { get; }

    public LootTable(string id, IReadOnlyList<LootEntry> entries)
    {
        Id = id;
        Entries = entries;
    }
}

public sealed class RewardSummary
{
    public long Gold { get; set; }
    public long Exp { get; set; }
    // 期望件数（或抽样得到的件数），key=itemId
    public Dictionary<string, double> Items { get; } = new();
}