using BlazorIdle.Shared.Models;

namespace BlazorIdle.Client.Services;

/// <summary>
/// 装备筛选和排序服务
/// 用于装备列表的过滤、排序和搜索
/// </summary>
public class EquipmentFilterService
{
    /// <summary>
    /// 装备槽位类型（用于筛选）
    /// </summary>
    public enum SlotFilter
    {
        All,        // 全部
        Head,       // 头部
        Neck,       // 颈部
        Shoulder,   // 肩部
        Back,       // 背部
        Chest,      // 胸部
        Wrist,      // 手腕
        Hands,      // 手套
        Waist,      // 腰部
        Legs,       // 腿部
        Feet,       // 脚部
        Finger,     // 戒指
        Trinket,    // 饰品
        Weapon,     // 武器（主手+副手）
        Shield      // 盾牌
    }

    /// <summary>
    /// 装备品质类型（用于筛选）
    /// </summary>
    public enum RarityFilter
    {
        All,        // 全部
        Common,     // 普通
        Rare,       // 稀有
        Epic,       // 史诗
        Legendary   // 传说
    }

    /// <summary>
    /// 排序方式
    /// </summary>
    public enum SortBy
    {
        ItemLevel,      // 物品等级（降序）
        QualityScore,   // 装备评分（降序）
        Name,           // 名称（升序）
        Rarity,         // 品质（降序）
        Tier            // 品级（降序）
    }

    /// <summary>
    /// 筛选装备列表
    /// </summary>
    /// <param name="equipmentList">原始装备列表</param>
    /// <param name="slotFilter">槽位筛选</param>
    /// <param name="rarityFilter">品质筛选</param>
    /// <param name="profession">职业筛选（null表示不筛选）</param>
    /// <param name="minItemLevel">最低物品等级（null表示不限制）</param>
    /// <param name="searchText">搜索文本（名称搜索，null或空表示不搜索）</param>
    /// <returns>筛选后的装备列表</returns>
    public List<GearInstanceDto> FilterEquipment(
        IEnumerable<GearInstanceDto> equipmentList,
        SlotFilter slotFilter = SlotFilter.All,
        RarityFilter rarityFilter = RarityFilter.All,
        Profession? profession = null,
        int? minItemLevel = null,
        string? searchText = null)
    {
        var filtered = equipmentList.AsEnumerable();

        // 槽位筛选
        if (slotFilter != SlotFilter.All)
        {
            filtered = filtered.Where(e => MatchesSlotFilter(e, slotFilter));
        }

        // 品质筛选
        if (rarityFilter != RarityFilter.All)
        {
            filtered = filtered.Where(e => e.Rarity == rarityFilter.ToString());
        }

        // 职业筛选（需要职业限制信息，这里简化处理）
        // 实际应该调用EquipmentRestrictionHelper检查
        if (profession.HasValue)
        {
            // TODO: 集成职业限制检查
            // filtered = filtered.Where(e => CanEquipByProfession(e, profession.Value));
        }

        // 物品等级筛选
        if (minItemLevel.HasValue)
        {
            filtered = filtered.Where(e => e.ItemLevel >= minItemLevel.Value);
        }

        // 名称搜索
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.ToLower();
            filtered = filtered.Where(e => 
                e.Name.ToLower().Contains(search) ||
                (e.DefinitionId?.ToLower().Contains(search) ?? false));
        }

        return filtered.ToList();
    }

    /// <summary>
    /// 排序装备列表
    /// </summary>
    /// <param name="equipmentList">装备列表</param>
    /// <param name="sortBy">排序方式</param>
    /// <param name="descending">是否降序（默认true）</param>
    /// <returns>排序后的装备列表</returns>
    public List<GearInstanceDto> SortEquipment(
        IEnumerable<GearInstanceDto> equipmentList,
        SortBy sortBy = SortBy.ItemLevel,
        bool descending = true)
    {
        var sorted = equipmentList.AsEnumerable();

        sorted = sortBy switch
        {
            SortBy.ItemLevel => descending 
                ? sorted.OrderByDescending(e => e.ItemLevel)
                : sorted.OrderBy(e => e.ItemLevel),
            
            SortBy.QualityScore => descending
                ? sorted.OrderByDescending(e => e.QualityScore)
                : sorted.OrderBy(e => e.QualityScore),
            
            SortBy.Name => descending
                ? sorted.OrderByDescending(e => e.Name)
                : sorted.OrderBy(e => e.Name),
            
            SortBy.Rarity => descending
                ? sorted.OrderByDescending(e => GetRarityOrder(e.Rarity))
                : sorted.OrderBy(e => GetRarityOrder(e.Rarity)),
            
            SortBy.Tier => descending
                ? sorted.OrderByDescending(e => e.Tier)
                : sorted.OrderBy(e => e.Tier),
            
            _ => sorted
        };

        return sorted.ToList();
    }

    /// <summary>
    /// 筛选并排序装备（组合方法）
    /// </summary>
    /// <param name="equipmentList">原始装备列表</param>
    /// <param name="slotFilter">槽位筛选</param>
    /// <param name="rarityFilter">品质筛选</param>
    /// <param name="sortBy">排序方式</param>
    /// <param name="profession">职业筛选</param>
    /// <param name="minItemLevel">最低物品等级</param>
    /// <param name="searchText">搜索文本</param>
    /// <returns>筛选并排序后的装备列表</returns>
    public List<GearInstanceDto> FilterAndSort(
        IEnumerable<GearInstanceDto> equipmentList,
        SlotFilter slotFilter = SlotFilter.All,
        RarityFilter rarityFilter = RarityFilter.All,
        SortBy sortBy = SortBy.ItemLevel,
        Profession? profession = null,
        int? minItemLevel = null,
        string? searchText = null)
    {
        var filtered = FilterEquipment(
            equipmentList, 
            slotFilter, 
            rarityFilter, 
            profession, 
            minItemLevel, 
            searchText);
        
        return SortEquipment(filtered, sortBy);
    }

    /// <summary>
    /// 判断装备是否匹配槽位筛选
    /// </summary>
    private bool MatchesSlotFilter(GearInstanceDto equipment, SlotFilter filter)
    {
        // TODO: 需要装备槽位信息，这里根据装备类型和武器类型推断
        // 实际应该在GearInstanceDto中添加SlotType字段
        
        return filter switch
        {
            SlotFilter.Weapon => IsWeapon(equipment) && !IsShield(equipment),
            SlotFilter.Shield => IsShield(equipment),
            SlotFilter.Finger => equipment.Name.Contains("戒指") || equipment.DefinitionId?.Contains("ring") == true,
            SlotFilter.Trinket => equipment.Name.Contains("饰品") || equipment.DefinitionId?.Contains("trinket") == true,
            // 其他槽位需要更多信息
            _ => true
        };
    }

    /// <summary>
    /// 判断是否为武器
    /// </summary>
    private bool IsWeapon(GearInstanceDto equipment)
    {
        return !string.IsNullOrEmpty(equipment.WeaponType) && 
               equipment.WeaponType != "None" &&
               equipment.WeaponType != "Shield";
    }

    /// <summary>
    /// 判断是否为盾牌
    /// </summary>
    private bool IsShield(GearInstanceDto equipment)
    {
        return equipment.WeaponType == "Shield";
    }

    /// <summary>
    /// 获取品质的排序权重
    /// </summary>
    private int GetRarityOrder(string rarity)
    {
        return rarity switch
        {
            "Legendary" => 4,
            "Epic" => 3,
            "Rare" => 2,
            "Common" => 1,
            _ => 0
        };
    }

    /// <summary>
    /// 获取槽位筛选的显示名称
    /// </summary>
    public static string GetSlotFilterName(SlotFilter filter)
    {
        return filter switch
        {
            SlotFilter.All => "全部",
            SlotFilter.Head => "头部",
            SlotFilter.Neck => "颈部",
            SlotFilter.Shoulder => "肩部",
            SlotFilter.Back => "背部",
            SlotFilter.Chest => "胸部",
            SlotFilter.Wrist => "手腕",
            SlotFilter.Hands => "手套",
            SlotFilter.Waist => "腰部",
            SlotFilter.Legs => "腿部",
            SlotFilter.Feet => "脚部",
            SlotFilter.Finger => "戒指",
            SlotFilter.Trinket => "饰品",
            SlotFilter.Weapon => "武器",
            SlotFilter.Shield => "盾牌",
            _ => filter.ToString()
        };
    }

    /// <summary>
    /// 获取品质筛选的显示名称
    /// </summary>
    public static string GetRarityFilterName(RarityFilter filter)
    {
        return filter switch
        {
            RarityFilter.All => "全部",
            RarityFilter.Common => "普通",
            RarityFilter.Rare => "稀有",
            RarityFilter.Epic => "史诗",
            RarityFilter.Legendary => "传说",
            _ => filter.ToString()
        };
    }

    /// <summary>
    /// 获取排序方式的显示名称
    /// </summary>
    public static string GetSortByName(SortBy sort)
    {
        return sort switch
        {
            SortBy.ItemLevel => "物品等级",
            SortBy.QualityScore => "装备评分",
            SortBy.Name => "名称",
            SortBy.Rarity => "品质",
            SortBy.Tier => "品级",
            _ => sort.ToString()
        };
    }
}
