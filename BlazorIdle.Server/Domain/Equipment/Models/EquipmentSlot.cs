namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 装备槽位枚举 - 定义17个装备槽位
/// </summary>
public enum EquipmentSlot
{
    /// <summary>头部 - 头盔</summary>
    Head,
    
    /// <summary>颈部 - 项链</summary>
    Neck,
    
    /// <summary>肩部 - 护肩</summary>
    Shoulder,
    
    /// <summary>背部 - 披风</summary>
    Back,
    
    /// <summary>胸部 - 胸甲</summary>
    Chest,
    
    /// <summary>手腕 - 护腕</summary>
    Wrist,
    
    /// <summary>手部 - 手套</summary>
    Hands,
    
    /// <summary>腰部 - 腰带</summary>
    Waist,
    
    /// <summary>腿部 - 护腿</summary>
    Legs,
    
    /// <summary>脚部 - 靴子</summary>
    Feet,
    
    /// <summary>戒指1</summary>
    Finger1,
    
    /// <summary>戒指2</summary>
    Finger2,
    
    /// <summary>饰品1</summary>
    Trinket1,
    
    /// <summary>饰品2</summary>
    Trinket2,
    
    /// <summary>主手武器</summary>
    MainHand,
    
    /// <summary>副手武器或盾牌</summary>
    OffHand,
    
    /// <summary>双手武器（占用主手和副手槽位）</summary>
    TwoHand
}
