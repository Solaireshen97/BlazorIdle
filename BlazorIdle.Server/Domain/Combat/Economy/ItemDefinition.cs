namespace BlazorIdle.Server.Domain.Economy;

public sealed class ItemDefinition
{
    public string Id { get; }
    public string Name { get; }

    public ItemDefinition(string id, string name)
    {
        Id = id;
        Name = name;
    }
}