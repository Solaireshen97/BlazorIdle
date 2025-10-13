namespace BlazorIdle.Shared.Models.Shop;

/// <summary>
/// 商店系统错误码
/// </summary>
public enum ShopErrorCode
{
    /// <summary>
    /// 无错误（成功）
    /// </summary>
    None = 0,

    #region 通用错误 (1xxx)
    
    /// <summary>
    /// 未知错误
    /// </summary>
    UnknownError = 1000,
    
    /// <summary>
    /// 参数无效
    /// </summary>
    InvalidParameter = 1001,
    
    /// <summary>
    /// 数据未找到
    /// </summary>
    NotFound = 1002,
    
    #endregion

    #region 角色相关错误 (2xxx)
    
    /// <summary>
    /// 角色 ID 格式错误
    /// </summary>
    InvalidCharacterId = 2001,
    
    /// <summary>
    /// 角色不存在
    /// </summary>
    CharacterNotFound = 2002,
    
    #endregion

    #region 商店相关错误 (3xxx)
    
    /// <summary>
    /// 商店不存在
    /// </summary>
    ShopNotFound = 3001,
    
    /// <summary>
    /// 商店未解锁
    /// </summary>
    ShopNotUnlocked = 3002,
    
    /// <summary>
    /// 商店已禁用
    /// </summary>
    ShopDisabled = 3003,
    
    #endregion

    #region 商品相关错误 (4xxx)
    
    /// <summary>
    /// 商品不存在
    /// </summary>
    ItemNotFound = 4001,
    
    /// <summary>
    /// 商品已禁用
    /// </summary>
    ItemDisabled = 4002,
    
    /// <summary>
    /// 商品库存不足
    /// </summary>
    InsufficientStock = 4003,
    
    #endregion

    #region 购买验证错误 (5xxx)
    
    /// <summary>
    /// 购买数量无效
    /// </summary>
    InvalidQuantity = 5001,
    
    /// <summary>
    /// 等级不足
    /// </summary>
    InsufficientLevel = 5002,
    
    /// <summary>
    /// 金币不足
    /// </summary>
    InsufficientGold = 5003,
    
    /// <summary>
    /// 货币物品不足
    /// </summary>
    InsufficientCurrency = 5004,
    
    /// <summary>
    /// 已达购买限制
    /// </summary>
    PurchaseLimitReached = 5005,
    
    /// <summary>
    /// 价格配置错误
    /// </summary>
    InvalidPrice = 5006,
    
    #endregion

    #region 库存相关错误 (6xxx)
    
    /// <summary>
    /// 背包已满
    /// </summary>
    InventoryFull = 6001,
    
    /// <summary>
    /// 物品添加失败
    /// </summary>
    ItemAddFailed = 6002,
    
    /// <summary>
    /// 物品扣除失败
    /// </summary>
    ItemDeductFailed = 6003,
    
    #endregion

    #region 系统错误 (9xxx)
    
    /// <summary>
    /// 数据库错误
    /// </summary>
    DatabaseError = 9001,
    
    /// <summary>
    /// 事务失败
    /// </summary>
    TransactionFailed = 9002,
    
    /// <summary>
    /// 配置错误
    /// </summary>
    ConfigurationError = 9003,
    
    #endregion
}
