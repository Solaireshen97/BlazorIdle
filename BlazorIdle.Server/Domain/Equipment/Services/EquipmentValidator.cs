using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备验证服务
/// 负责验证装备需求（等级、职业、武器类型等）
/// </summary>
public class EquipmentValidator
{
    /// <summary>
    /// 验证角色是否可以装备该装备
    /// </summary>
    /// <param name="character">角色</param>
    /// <param name="gear">装备实例</param>
    /// <param name="definition">装备定义</param>
    /// <returns>验证结果</returns>
    public ValidationResult CanEquip(Character character, GearInstance gear, GearDefinition definition)
    {
        // 1. 验证等级需求
        if (character.Level < definition.RequiredLevel)
        {
            return ValidationResult.Fail($"需要等级 {definition.RequiredLevel}，当前等级 {character.Level}");
        }

        // 2. 验证护甲类型限制
        if (definition.ArmorType != ArmorType.None)
        {
            if (!CanEquipArmorType(character.Profession, definition.ArmorType))
            {
                return ValidationResult.Fail($"{GetProfessionName(character.Profession)}无法装备{GetArmorTypeName(definition.ArmorType)}");
            }
        }

        // 3. 验证武器类型限制
        if (definition.WeaponType != WeaponType.None)
        {
            if (!CanEquipWeaponType(character.Profession, definition.WeaponType))
            {
                return ValidationResult.Fail($"{GetProfessionName(character.Profession)}无法装备{GetWeaponTypeName(definition.WeaponType)}");
            }
        }

        // 4. 验证双持能力（如果是副手武器）
        if (definition.Slot == EquipmentSlot.OffHand && definition.WeaponType != WeaponType.None && definition.WeaponType != WeaponType.Shield)
        {
            if (!CanDualWield(character.Profession))
            {
                return ValidationResult.Fail($"{GetProfessionName(character.Profession)}无法双持武器");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// 验证职业是否可以装备该护甲类型
    /// </summary>
    private bool CanEquipArmorType(Profession profession, ArmorType armorType)
    {
        return profession switch
        {
            Profession.Warrior => armorType is ArmorType.Plate or ArmorType.Mail or ArmorType.Leather or ArmorType.Cloth,
            Profession.Ranger => armorType is ArmorType.Mail or ArmorType.Leather or ArmorType.Cloth,
            // 未来扩展：Mage、Rogue、Priest、Shaman、Paladin
            _ => true // 未知职业默认允许（便于测试和扩展）
        };
    }

    /// <summary>
    /// 验证职业是否可以装备该武器类型
    /// </summary>
    private bool CanEquipWeaponType(Profession profession, WeaponType weaponType)
    {
        return profession switch
        {
            Profession.Warrior => weaponType is WeaponType.Sword or WeaponType.Axe or WeaponType.Mace 
                or WeaponType.TwoHandSword or WeaponType.TwoHandAxe or WeaponType.TwoHandMace 
                or WeaponType.Polearm or WeaponType.Shield,
            
            Profession.Ranger => weaponType is WeaponType.Sword or WeaponType.Dagger or WeaponType.Axe 
                or WeaponType.Bow or WeaponType.Crossbow or WeaponType.Gun,
            
            // 未来扩展：其他职业
            _ => true // 未知职业默认允许
        };
    }

    /// <summary>
    /// 验证职业是否可以双持
    /// </summary>
    private bool CanDualWield(Profession profession)
    {
        return profession switch
        {
            Profession.Warrior => true,
            Profession.Ranger => true,
            // 未来扩展：其他职业
            _ => false
        };
    }

    /// <summary>
    /// 获取职业名称（中文）
    /// </summary>
    private string GetProfessionName(Profession profession)
    {
        return profession switch
        {
            Profession.Warrior => "战士",
            Profession.Ranger => "游侠",
            // 未来扩展：其他职业名称
            _ => profession.ToString()
        };
    }

    /// <summary>
    /// 获取护甲类型名称（中文）
    /// </summary>
    private string GetArmorTypeName(ArmorType armorType)
    {
        return armorType switch
        {
            ArmorType.Cloth => "布甲",
            ArmorType.Leather => "皮甲",
            ArmorType.Mail => "锁甲",
            ArmorType.Plate => "板甲",
            _ => armorType.ToString()
        };
    }

    /// <summary>
    /// 获取武器类型名称（中文）
    /// </summary>
    private string GetWeaponTypeName(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Sword => "剑",
            WeaponType.Dagger => "匕首",
            WeaponType.Axe => "斧",
            WeaponType.Mace => "锤",
            WeaponType.Fist => "拳套",
            WeaponType.Wand => "魔杖",
            WeaponType.TwoHandSword => "双手剑",
            WeaponType.TwoHandAxe => "双手斧",
            WeaponType.TwoHandMace => "双手锤",
            WeaponType.Staff => "法杖",
            WeaponType.Polearm => "长柄武器",
            WeaponType.Bow => "弓",
            WeaponType.Crossbow => "弩",
            WeaponType.Gun => "枪",
            WeaponType.Shield => "盾牌",
            _ => weaponType.ToString()
        };
    }
}

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = "";

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Fail(string message) => new() { IsValid = false, Message = message };
}
