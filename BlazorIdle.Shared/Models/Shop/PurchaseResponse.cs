namespace BlazorIdle.Shared.Models.Shop;

/// <summary>
/// 购买响应 DTO
/// </summary>
public class PurchaseResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public PurchaseResultDto? Result { get; set; }
}

public class PurchaseResultDto
{
    public Guid PurchaseRecordId { get; set; }
    public int RemainingGold { get; set; }
    public int ItemQuantity { get; set; }
    public DateTime PurchasedAt { get; set; }
}
