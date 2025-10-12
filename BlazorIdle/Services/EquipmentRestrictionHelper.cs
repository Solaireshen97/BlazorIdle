using BlazorIdle.Client.Services;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Client.Services;

/// <summary>
/// 装备限制辅助服务
/// 用于判断装备是否满足职业和等级要求
/// </summary>
public class EquipmentRestrictionHelper
{
    /// <summary>
    /// 限制检查结果
    /// </summary>
    public class RestrictionCheckResult
    {
        /// <summary>是否可以装备</summary>
        public bool CanEquip { get; set; }
        
        /// <summary>不能装备的原因列表</summary>
        public List<string> Reasons { get; set; } = new();
        
        /// <summary>所需职业（如果有限制）</summary>
        public List<string>? RequiredProfessions { get; set; }
        
        /// <summary>所需等级（如果有限制）</summary>
        public int? RequiredLevel { get; set; }
    }

    /// <summary>
    /// 检查装备限制
    /// </summary>
    /// <param name="gear">装备实例</param>
    /// <param name="characterProfession">角色职业</param>
    /// <param name="characterLevel">角色等级</param>
    /// <returns>限制检查结果</returns>
    public RestrictionCheckResult CheckRestrictions(
        GearInstanceDto gear, 
        Profession characterProfession, 
        int characterLevel)
    {
        var result = new RestrictionCheckResult
        {
            CanEquip = true
        };

        // 检查等级要求
        if (gear.ItemLevel > characterLevel)
        {
            result.CanEquip = false;
            result.RequiredLevel = gear.ItemLevel;
            result.Reasons.Add($"需要等级 {gear.ItemLevel}（当前 {characterLevel}）");
        }

        // 检查护甲类型限制
        if (!string.IsNullOrEmpty(gear.ArmorType) && gear.ArmorType != "None")
        {
            if (!CanEquipArmorType(characterProfession, gear.ArmorType))
            {
                result.CanEquip = false;
                var allowedProfessions = GetAllowedProfessionsForArmorType(gear.ArmorType);
                result.RequiredProfessions = allowedProfessions;
                result.Reasons.Add($"需要职业: {string.Join(", ", allowedProfessions.Select(GetProfessionName))}");
            }
        }

        // 检查武器类型限制
        if (!string.IsNullOrEmpty(gear.WeaponType) && gear.WeaponType != "None")
        {
            if (!CanEquipWeaponType(characterProfession, gear.WeaponType))
            {
                result.CanEquip = false;
                var allowedProfessions = GetAllowedProfessionsForWeaponType(gear.WeaponType);
                result.RequiredProfessions = allowedProfessions;
                result.Reasons.Add($"需要职业: {string.Join(", ", allowedProfessions.Select(GetProfessionName))}");
            }
        }

        return result;
    }

    /// <summary>
    /// 判断职业是否可以装备指定护甲类型
    /// </summary>
    private bool CanEquipArmorType(Profession profession, string armorType)
    {
        return profession switch
        {
            Profession.Warrior => armorType is "Plate" or "Mail" or "Leather" or "Cloth",
            Profession.Ranger => armorType is "Mail" or "Leather" or "Cloth",
            _ => false
        };
    }

    /// <summary>
    /// 判断职业是否可以装备指定武器类型
    /// </summary>
    private bool CanEquipWeaponType(Profession profession, string weaponType)
    {
        return profession switch
        {
            Profession.Warrior => weaponType is "Sword" or "Axe" or "Mace" or "Fist" or 
                                                "TwoHandSword" or "TwoHandAxe" or "TwoHandMace" or 
                                                "Polearm" or "Shield",
            Profession.Ranger => weaponType is "Bow" or "Crossbow" or "Gun" or 
                                               "Dagger" or "Sword" or "Axe" or "Fist",
            _ => false
        };
    }

    /// <summary>
    /// 获取可以装备指定护甲类型的职业列表
    /// </summary>
    private List<string> GetAllowedProfessionsForArmorType(string armorType)
    {
        return armorType switch
        {
            "Plate" => new List<string> { "Warrior" },
            "Mail" => new List<string> { "Warrior", "Ranger" },
            "Leather" => new List<string> { "Warrior", "Ranger" },
            "Cloth" => new List<string> { "Warrior", "Ranger" },
            _ => new List<string>()
        };
    }

    /// <summary>
    /// 获取可以装备指定武器类型的职业列表
    /// </summary>
    private List<string> GetAllowedProfessionsForWeaponType(string weaponType)
    {
        return weaponType switch
        {
            "Sword" or "Axe" or "Mace" or "Fist" or "TwoHandSword" or "TwoHandAxe" or 
            "TwoHandMace" or "Polearm" or "Shield" 
                => new List<string> { "Warrior" },
            
            "Bow" or "Crossbow" or "Gun" or "Dagger" 
                => new List<string> { "Ranger" },
            
            _ => new List<string>()
        };
    }

    /// <summary>
    /// 获取职业的中文名称
    /// </summary>
    private string GetProfessionName(string profession)
    {
        return profession switch
        {
            "Warrior" => "战士",
            "Ranger" => "游侠",
            _ => profession
        };
    }

    /// <summary>
    /// 获取限制提示的颜色
    /// </summary>
    /// <param name="canEquip">是否可装备</param>
    /// <returns>CSS颜色值</returns>
    public string GetRestrictionColor(bool canEquip)
    {
        return canEquip ? "#4caf50" : "#f44336";
    }

    /// <summary>
    /// 获取限制提示图标
    /// </summary>
    /// <param name="canEquip">是否可装备</param>
    /// <returns>图标字符</returns>
    public string GetRestrictionIcon(bool canEquip)
    {
        return canEquip ? "✓" : "✗";
    }

    /// <summary>
    /// 生成限制提示文本
    /// </summary>
    /// <param name="result">检查结果</param>
    /// <returns>格式化的提示文本</returns>
    public string GenerateRestrictionTooltip(RestrictionCheckResult result)
    {
        if (result.CanEquip)
        {
            return "✓ 可以装备此物品";
        }

        var tooltip = "✗ 无法装备此物品\n\n原因:\n";
        foreach (var reason in result.Reasons)
        {
            tooltip += $"  • {reason}\n";
        }

        return tooltip.TrimEnd();
    }
}
