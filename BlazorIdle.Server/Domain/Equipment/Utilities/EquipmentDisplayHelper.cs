using BlazorIdle.Server.Domain.Equipment.Configuration;
using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.Utilities;

/// <summary>
/// 装备显示辅助类
/// 提供装备相关的格式化和显示工具方法
/// </summary>
public static class EquipmentDisplayHelper
{
    /// <summary>
    /// 获取稀有度显示名称
    /// </summary>
    /// <param name="rarity">稀有度</param>
    /// <returns>中文名称</returns>
    public static string GetRarityName(Rarity rarity)
    {
        return EquipmentSystemConfig.RarityConfig.RarityNames.TryGetValue(rarity, out var name)
            ? name
            : "未知";
    }

    /// <summary>
    /// 获取稀有度颜色代码（用于UI显示）
    /// </summary>
    /// <param name="rarity">稀有度</param>
    /// <returns>十六进制颜色代码</returns>
    public static string GetRarityColor(Rarity rarity)
    {
        return EquipmentSystemConfig.RarityConfig.RarityColors.TryGetValue(rarity, out var color)
            ? color
            : "#ffffff";
    }

    /// <summary>
    /// 获取品级显示文本
    /// </summary>
    /// <param name="tierLevel">品级等级</param>
    /// <returns>品级文本（如"T1"）</returns>
    public static string GetTierDisplay(int tierLevel)
    {
        if (tierLevel < EquipmentSystemConfig.TierConfig.MinTier)
            return $"T{EquipmentSystemConfig.TierConfig.MinTier}";
        
        if (tierLevel > EquipmentSystemConfig.TierConfig.MaxTier)
            return $"T{EquipmentSystemConfig.TierConfig.MaxTier}";
        
        return $"T{tierLevel}";
    }

    /// <summary>
    /// 获取槽位显示名称
    /// </summary>
    /// <param name="slot">装备槽位</param>
    /// <returns>中文名称</returns>
    public static string GetSlotName(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Head => "头部",
            EquipmentSlot.Neck => "颈部",
            EquipmentSlot.Shoulder => "肩部",
            EquipmentSlot.Back => "背部",
            EquipmentSlot.Chest => "胸部",
            EquipmentSlot.Wrist => "腕部",
            EquipmentSlot.Hands => "手部",
            EquipmentSlot.Waist => "腰部",
            EquipmentSlot.Legs => "腿部",
            EquipmentSlot.Feet => "脚部",
            EquipmentSlot.Finger1 => "戒指1",
            EquipmentSlot.Finger2 => "戒指2",
            EquipmentSlot.Trinket1 => "饰品1",
            EquipmentSlot.Trinket2 => "饰品2",
            EquipmentSlot.MainHand => "主手",
            EquipmentSlot.OffHand => "副手",
            EquipmentSlot.TwoHand => "双手",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取护甲类型显示名称
    /// </summary>
    /// <param name="armorType">护甲类型</param>
    /// <returns>中文名称</returns>
    public static string GetArmorTypeName(ArmorType armorType)
    {
        return armorType switch
        {
            ArmorType.Cloth => "布甲",
            ArmorType.Leather => "皮甲",
            ArmorType.Mail => "锁甲",
            ArmorType.Plate => "板甲",
            ArmorType.None => "无护甲",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取武器类型显示名称
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>中文名称</returns>
    public static string GetWeaponTypeName(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Sword => "单手剑",
            WeaponType.Axe => "单手斧",
            WeaponType.Mace => "单手锤",
            WeaponType.Dagger => "匕首",
            WeaponType.Fist => "拳套",
            WeaponType.TwoHandSword => "双手剑",
            WeaponType.TwoHandAxe => "双手斧",
            WeaponType.TwoHandMace => "双手锤",
            WeaponType.Polearm => "长柄武器",
            WeaponType.Staff => "法杖",
            WeaponType.Bow => "弓",
            WeaponType.Crossbow => "弩",
            WeaponType.Gun => "枪",
            WeaponType.Wand => "魔杖",
            WeaponType.Shield => "盾牌",
            WeaponType.None => "无武器",
            _ => "未知"
        };
    }

    /// <summary>
    /// 格式化属性值显示
    /// </summary>
    /// <param name="statType">属性类型</param>
    /// <param name="value">属性值</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatStatValue(StatType statType, double value)
    {
        // 百分比属性
        if (IsPercentageStat(statType))
        {
            return $"{value:F1}%";
        }

        // 整数属性
        return $"{(int)Math.Round(value)}";
    }

    /// <summary>
    /// 判断属性是否为百分比属性
    /// </summary>
    /// <param name="statType">属性类型</param>
    /// <returns>是否为百分比</returns>
    public static bool IsPercentageStat(StatType statType)
    {
        return statType switch
        {
            StatType.CritChance => true,
            StatType.HastePercent => true,
            StatType.BlockChance => true,
            _ => false
        };
    }

    /// <summary>
    /// 获取装备完整描述文本
    /// </summary>
    /// <param name="gear">装备实例</param>
    /// <returns>装备描述</returns>
    public static string GetGearDescription(GearInstance gear)
    {
        if (gear == null || gear.Definition == null)
            return "未知装备";

        var parts = new List<string>
        {
            $"{GetTierDisplay(gear.TierLevel)}",
            $"{GetRarityName(gear.Rarity)}",
            gear.Definition.Name,
            $"({GetSlotName(gear.Definition.Slot)})"
        };

        if (gear.Definition.ArmorType != ArmorType.None)
        {
            parts.Add($"- {GetArmorTypeName(gear.Definition.ArmorType)}");
        }

        if (gear.Definition.WeaponType != WeaponType.None)
        {
            parts.Add($"- {GetWeaponTypeName(gear.Definition.WeaponType)}");
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// 获取装备评分等级描述
    /// </summary>
    /// <param name="qualityScore">装备评分</param>
    /// <returns>评级描述</returns>
    public static string GetQualityRating(int qualityScore)
    {
        return qualityScore switch
        {
            < 100 => "一般",
            < 200 => "良好",
            < 400 => "优秀",
            < 700 => "卓越",
            < 1000 => "完美",
            _ => "传奇"
        };
    }

    /// <summary>
    /// 格式化材料数量显示
    /// </summary>
    /// <param name="materials">材料字典</param>
    /// <returns>格式化的材料列表</returns>
    public static string FormatMaterials(Dictionary<string, int> materials)
    {
        if (materials == null || materials.Count == 0)
            return "无";

        return string.Join(", ", materials.Select(m => $"{GetMaterialName(m.Key)} x{m.Value}"));
    }

    /// <summary>
    /// 获取材料显示名称
    /// </summary>
    /// <param name="materialId">材料ID</param>
    /// <returns>材料名称</returns>
    private static string GetMaterialName(string materialId)
    {
        return materialId switch
        {
            "material_cloth" => "布料",
            "material_leather" => "皮革",
            "material_mail" => "锁甲片",
            "material_plate" => "板甲片",
            "material_weapon" => "武器材料",
            "material_generic" => "通用材料",
            "essence_rare" => "稀有精华",
            "essence_epic" => "史诗精华",
            "essence_legendary" => "传说精华",
            "essence_tier" => "品级精华",
            "material_essence" => "重铸精华",
            "gold" => "金币",
            _ => materialId
        };
    }

    /// <summary>
    /// 获取属性类型显示名称
    /// </summary>
    /// <param name="statType">属性类型</param>
    /// <returns>中文名称</returns>
    public static string GetStatTypeName(StatType statType)
    {
        return statType switch
        {
            StatType.Strength => "力量",
            StatType.Agility => "敏捷",
            StatType.Intellect => "智力",
            StatType.Stamina => "耐力",
            StatType.AttackPower => "攻击强度",
            StatType.SpellPower => "法术强度",
            StatType.CritRating => "暴击等级",
            StatType.CritChance => "暴击率",
            StatType.Haste => "急速等级",
            StatType.HastePercent => "急速",
            StatType.Armor => "护甲",
            StatType.BlockRating => "格挡等级",
            StatType.BlockChance => "格挡率",
            StatType.HitRating => "命中等级",
            StatType.MasteryRating => "精通等级",
            StatType.DodgeRating => "闪避等级",
            StatType.ParryRating => "招架等级",
            StatType.Health => "生命值",
            StatType.Mana => "法力值",
            _ => statType.ToString()
        };
    }

    /// <summary>
    /// 格式化装备简要信息（单行显示）
    /// </summary>
    /// <param name="gear">装备实例</param>
    /// <returns>简要信息字符串</returns>
    public static string GetGearSummary(GearInstance? gear)
    {
        if (gear == null || gear.Definition == null)
            return "空";

        return $"{GetRarityName(gear.Rarity)} {gear.Definition.Name} (物品等级{gear.ItemLevel})";
    }

    /// <summary>
    /// 比较两个稀有度的等级（用于排序）
    /// </summary>
    /// <param name="rarity1">稀有度1</param>
    /// <param name="rarity2">稀有度2</param>
    /// <returns>比较结果（负数表示rarity1更低，0表示相等，正数表示rarity1更高）</returns>
    public static int CompareRarity(Rarity rarity1, Rarity rarity2)
    {
        return GetRarityOrder(rarity1) - GetRarityOrder(rarity2);
    }

    /// <summary>
    /// 获取稀有度的排序值
    /// </summary>
    /// <param name="rarity">稀有度</param>
    /// <returns>排序值（数值越大表示稀有度越高）</returns>
    private static int GetRarityOrder(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Common => 0,
            Rarity.Rare => 1,
            Rarity.Epic => 2,
            Rarity.Legendary => 3,
            _ => -1
        };
    }

    /// <summary>
    /// 判断装备是否为武器
    /// </summary>
    /// <param name="slot">装备槽位</param>
    /// <returns>是否为武器槽位</returns>
    public static bool IsWeaponSlot(EquipmentSlot slot)
    {
        return slot is EquipmentSlot.MainHand or EquipmentSlot.OffHand or EquipmentSlot.TwoHand;
    }

    /// <summary>
    /// 判断装备是否为护甲
    /// </summary>
    /// <param name="slot">装备槽位</param>
    /// <returns>是否为护甲槽位</returns>
    public static bool IsArmorSlot(EquipmentSlot slot)
    {
        return slot is EquipmentSlot.Head or EquipmentSlot.Shoulder or EquipmentSlot.Chest 
            or EquipmentSlot.Wrist or EquipmentSlot.Hands or EquipmentSlot.Waist 
            or EquipmentSlot.Legs or EquipmentSlot.Feet;
    }

    /// <summary>
    /// 判断装备是否为饰品
    /// </summary>
    /// <param name="slot">装备槽位</param>
    /// <returns>是否为饰品槽位</returns>
    public static bool IsAccessorySlot(EquipmentSlot slot)
    {
        return slot is EquipmentSlot.Neck or EquipmentSlot.Back 
            or EquipmentSlot.Finger1 or EquipmentSlot.Finger2 
            or EquipmentSlot.Trinket1 or EquipmentSlot.Trinket2;
    }
}
