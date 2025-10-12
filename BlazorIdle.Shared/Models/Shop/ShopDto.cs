namespace BlazorIdle.Shared.Models.Shop;

/// <summary>
/// 商店 DTO
/// </summary>
public class ShopDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsUnlocked { get; set; }
    public string? UnlockHint { get; set; }
    public int ItemCount { get; set; }
}
