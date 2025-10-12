namespace BlazorIdle.Server.Domain.Equipment.Configuration;

/// <summary>
/// 装备系统配置
/// 集中管理装备系统的常量和配置参数
/// </summary>
public static class EquipmentSystemConfig
{
    /// <summary>
    /// 品级配置
    /// </summary>
    public static class TierConfig
    {
        /// <summary>最小品级</summary>
        public const int MinTier = 1;
        
        /// <summary>最大品级</summary>
        public const int MaxTier = 3;
        
        /// <summary>品级系数映射</summary>
        public static readonly Dictionary<int, double> TierMultipliers = new()
        {
            { 1, 0.8 },
            { 2, 1.0 },
            { 3, 1.2 }
        };
        
        /// <summary>
        /// 获取品级系数
        /// </summary>
        /// <param name="tierLevel">品级等级</param>
        /// <returns>品级系数</returns>
        public static double GetMultiplier(int tierLevel)
        {
            return TierMultipliers.TryGetValue(tierLevel, out var multiplier) 
                ? multiplier 
                : 1.0;
        }
    }
    
    /// <summary>
    /// 稀有度配置
    /// </summary>
    public static class RarityConfig
    {
        /// <summary>默认稀有度权重</summary>
        public static readonly Dictionary<Models.Rarity, double> DefaultWeights = new()
        {
            { Models.Rarity.Common, 50.0 },
            { Models.Rarity.Rare, 30.0 },
            { Models.Rarity.Epic, 15.0 },
            { Models.Rarity.Legendary, 5.0 }
        };
        
        /// <summary>稀有度名称映射</summary>
        public static readonly Dictionary<Models.Rarity, string> RarityNames = new()
        {
            { Models.Rarity.Common, "普通" },
            { Models.Rarity.Rare, "稀有" },
            { Models.Rarity.Epic, "史诗" },
            { Models.Rarity.Legendary, "传说" }
        };
        
        /// <summary>稀有度颜色映射（用于UI显示）</summary>
        public static readonly Dictionary<Models.Rarity, string> RarityColors = new()
        {
            { Models.Rarity.Common, "#9d9d9d" },
            { Models.Rarity.Rare, "#0070dd" },
            { Models.Rarity.Epic, "#a335ee" },
            { Models.Rarity.Legendary, "#ff8000" }
        };
    }
    
    /// <summary>
    /// 物品等级配置
    /// </summary>
    public static class ItemLevelConfig
    {
        /// <summary>稀有度物品等级加成</summary>
        public static readonly Dictionary<Models.Rarity, int> RarityBonus = new()
        {
            { Models.Rarity.Common, 0 },
            { Models.Rarity.Rare, 2 },
            { Models.Rarity.Epic, 5 },
            { Models.Rarity.Legendary, 10 }
        };
    }
    
    /// <summary>
    /// 分解配置
    /// </summary>
    public static class DisenchantConfig
    {
        /// <summary>材料类型映射</summary>
        public static readonly Dictionary<Models.ArmorType, string> ArmorMaterials = new()
        {
            { Models.ArmorType.Cloth, "material_cloth" },
            { Models.ArmorType.Leather, "material_leather" },
            { Models.ArmorType.Mail, "material_mail" },
            { Models.ArmorType.Plate, "material_plate" },
            { Models.ArmorType.None, "material_generic" }
        };
        
        /// <summary>武器材料ID</summary>
        public const string WeaponMaterial = "material_weapon";
        
        /// <summary>稀有材料映射</summary>
        public static readonly Dictionary<Models.Rarity, string?> RareMaterials = new()
        {
            { Models.Rarity.Common, null },
            { Models.Rarity.Rare, "essence_rare" },
            { Models.Rarity.Epic, "essence_epic" },
            { Models.Rarity.Legendary, "essence_legendary" }
        };
        
        /// <summary>稀有材料数量</summary>
        public static readonly Dictionary<Models.Rarity, int> RareMaterialAmounts = new()
        {
            { Models.Rarity.Common, 0 },
            { Models.Rarity.Rare, 1 },
            { Models.Rarity.Epic, 3 },
            { Models.Rarity.Legendary, 10 }
        };
        
        /// <summary>品级材料ID</summary>
        public const string TierMaterial = "essence_tier";
        
        /// <summary>槽位材料倍率</summary>
        public static readonly Dictionary<Models.EquipmentSlot, double> SlotMultipliers = new()
        {
            { Models.EquipmentSlot.Chest, 1.5 },
            { Models.EquipmentSlot.TwoHand, 1.5 },
            { Models.EquipmentSlot.Legs, 1.3 }
        };
        
        /// <summary>
        /// 获取槽位倍率
        /// </summary>
        /// <param name="slot">装备槽位</param>
        /// <returns>倍率</returns>
        public static double GetSlotMultiplier(Models.EquipmentSlot slot)
        {
            return SlotMultipliers.TryGetValue(slot, out var multiplier) 
                ? multiplier 
                : 1.0;
        }
    }
    
    /// <summary>
    /// 重铸配置
    /// </summary>
    public static class ReforgeConfig
    {
        /// <summary>基础材料ID</summary>
        public const string EssenceMaterial = "material_essence";
        
        /// <summary>金币ID</summary>
        public const string Gold = "gold";
        
        /// <summary>稀有度成本倍率</summary>
        public static readonly Dictionary<Models.Rarity, double> RarityCostMultipliers = new()
        {
            { Models.Rarity.Common, 1.0 },
            { Models.Rarity.Rare, 2.0 },
            { Models.Rarity.Epic, 4.0 },
            { Models.Rarity.Legendary, 8.0 }
        };
        
        /// <summary>最小金币成本</summary>
        public const int MinGoldCost = 100;
        
        /// <summary>最小材料数量</summary>
        public const int MinMaterialAmount = 1;
    }
    
    /// <summary>
    /// 装备评分配置
    /// </summary>
    public static class QualityScoreConfig
    {
        /// <summary>基础属性权重</summary>
        public const double StatWeight = 0.1;
        
        /// <summary>词条属性权重</summary>
        public const double AffixWeight = 0.2;
        
        /// <summary>稀有度评分倍率</summary>
        public static readonly Dictionary<Models.Rarity, double> RarityScoreMultipliers = new()
        {
            { Models.Rarity.Common, 1.0 },
            { Models.Rarity.Rare, 1.5 },
            { Models.Rarity.Epic, 2.0 },
            { Models.Rarity.Legendary, 3.0 }
        };
    }
    
    /// <summary>
    /// 系统限制配置
    /// </summary>
    public static class Limits
    {
        /// <summary>批量分解最大数量</summary>
        public const int MaxBatchDisenchantSize = 50;
        
        /// <summary>词条最大数量</summary>
        public const int MaxAffixCount = 6;
        
        /// <summary>装备名称最大长度</summary>
        public const int MaxGearNameLength = 100;
    }
}
