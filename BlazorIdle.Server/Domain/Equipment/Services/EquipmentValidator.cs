using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备验证服务
/// 验证职业装备限制、等级需求、槽位兼容性等
/// </summary>
public class EquipmentValidator
{
    // 职业-护甲类型兼容性矩阵
    // 注: 当前仅实现Warrior和Ranger，其他职业预留
    private static readonly Dictionary<Profession, HashSet<ArmorType>> ProfessionArmorCompatibility = new()
    {
        {
            Profession.Warrior,
            new HashSet<ArmorType> { ArmorType.None, ArmorType.Plate, ArmorType.Mail, ArmorType.Leather, ArmorType.Cloth }
        },
        {
            Profession.Ranger,
            new HashSet<ArmorType> { ArmorType.None, ArmorType.Mail, ArmorType.Leather, ArmorType.Cloth }
        }
        // TODO: 未来添加更多职业
        // Rogue: Leather, Cloth
        // Mage: Cloth
        // Priest: Cloth
        // Paladin: Plate, Mail, Leather, Cloth
        // Druid: Leather, Cloth
    };

    // 职业-武器类型兼容性矩阵
    // 注: 当前仅实现Warrior和Ranger，其他职业预留
    private static readonly Dictionary<Profession, HashSet<WeaponType>> ProfessionWeaponCompatibility = new()
    {
        {
            Profession.Warrior,
            new HashSet<WeaponType>
            {
                WeaponType.Sword, WeaponType.Axe, WeaponType.Mace, WeaponType.Fist,
                WeaponType.TwoHandSword, WeaponType.TwoHandAxe, WeaponType.TwoHandMace,
                WeaponType.Polearm, WeaponType.Shield
            }
        },
        {
            Profession.Ranger,
            new HashSet<WeaponType>
            {
                WeaponType.Bow, WeaponType.Crossbow, WeaponType.Gun,
                WeaponType.Dagger, WeaponType.Sword, WeaponType.Axe, WeaponType.Fist
            }
        }
        // TODO: 未来添加更多职业
        // Rogue: Dagger, Sword, Fist, Axe, Mace
        // Mage: Wand, Staff, Dagger, Sword
        // Priest: Wand, Staff, Mace, Dagger
        // Paladin: Sword, Mace, Axe, TwoHand variants, Polearm, Shield
        // Druid: Staff, Mace, Dagger, Fist, Polearm
    };

    /// <summary>
    /// 验证职业是否可以装备该护甲类型
    /// </summary>
    public ValidationResult ValidateArmorType(Profession profession, ArmorType armorType)
    {
        if (!ProfessionArmorCompatibility.TryGetValue(profession, out var allowedArmor))
        {
            return ValidationResult.Failure($"未知职业: {profession}");
        }

        if (!allowedArmor.Contains(armorType))
        {
            return ValidationResult.Failure($"{GetProfessionName(profession)}无法装备{ArmorCalculator.GetArmorTypeName(armorType)}");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// 验证职业是否可以装备该武器类型
    /// </summary>
    public ValidationResult ValidateWeaponType(Profession profession, WeaponType weaponType)
    {
        if (weaponType == WeaponType.None)
        {
            return ValidationResult.Success();
        }

        if (!ProfessionWeaponCompatibility.TryGetValue(profession, out var allowedWeapons))
        {
            return ValidationResult.Failure($"未知职业: {profession}");
        }

        if (!allowedWeapons.Contains(weaponType))
        {
            return ValidationResult.Failure($"{GetProfessionName(profession)}无法装备{AttackSpeedCalculator.GetWeaponTypeName(weaponType)}");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// 验证等级需求
    /// </summary>
    public ValidationResult ValidateLevel(int characterLevel, int requiredLevel)
    {
        if (characterLevel < requiredLevel)
        {
            return ValidationResult.Failure($"需要等级 {requiredLevel}（当前等级 {characterLevel}）");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// 验证槽位兼容性
    /// </summary>
    public ValidationResult ValidateSlot(EquipmentSlot gearSlot, EquipmentSlot targetSlot)
    {
        // TwoHand可以装备到MainHand或TwoHand槽位
        if (gearSlot == EquipmentSlot.TwoHand && targetSlot == EquipmentSlot.MainHand)
        {
            return ValidationResult.Success();
        }

        // MainHand装备可以装备到MainHand或OffHand（双持）
        if (gearSlot == EquipmentSlot.MainHand && (targetSlot == EquipmentSlot.MainHand || targetSlot == EquipmentSlot.OffHand))
        {
            return ValidationResult.Success();
        }

        // OffHand装备只能装备到OffHand
        if (gearSlot == EquipmentSlot.OffHand && targetSlot != EquipmentSlot.OffHand)
        {
            return ValidationResult.Failure("副手装备只能装备到副手槽位");
        }

        // 其他装备必须匹配槽位
        if (gearSlot != targetSlot)
        {
            return ValidationResult.Failure($"该装备只能装备到{GetSlotName(gearSlot)}槽位");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// 完整验证装备是否可装备
    /// </summary>
    public ValidationResult ValidateEquip(
        GearDefinition definition,
        Profession profession,
        int characterLevel,
        EquipmentSlot targetSlot)
    {
        // 验证等级
        var levelResult = ValidateLevel(characterLevel, definition.RequiredLevel);
        if (!levelResult.IsSuccess)
        {
            return levelResult;
        }

        // 验证槽位
        var slotResult = ValidateSlot(definition.Slot, targetSlot);
        if (!slotResult.IsSuccess)
        {
            return slotResult;
        }

        // 验证护甲类型
        if (definition.ArmorType != ArmorType.None)
        {
            var armorResult = ValidateArmorType(profession, definition.ArmorType);
            if (!armorResult.IsSuccess)
            {
                return armorResult;
            }
        }

        // 验证武器类型
        if (definition.WeaponType != WeaponType.None)
        {
            var weaponResult = ValidateWeaponType(profession, definition.WeaponType);
            if (!weaponResult.IsSuccess)
            {
                return weaponResult;
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// 获取职业允许的护甲类型列表
    /// </summary>
    public static HashSet<ArmorType> GetAllowedArmorTypes(Profession profession)
    {
        return ProfessionArmorCompatibility.TryGetValue(profession, out var allowed) 
            ? allowed 
            : new HashSet<ArmorType> { ArmorType.None };
    }

    /// <summary>
    /// 获取职业允许的武器类型列表
    /// </summary>
    public static HashSet<WeaponType> GetAllowedWeaponTypes(Profession profession)
    {
        return ProfessionWeaponCompatibility.TryGetValue(profession, out var allowed) 
            ? allowed 
            : new HashSet<WeaponType> { WeaponType.None };
    }

    private static string GetProfessionName(Profession profession)
    {
        return profession switch
        {
            Profession.Warrior => "战士",
            Profession.Ranger => "游侠",
            _ => "未知职业"
        };
    }

    private static string GetSlotName(EquipmentSlot slot)
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
}

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }

    private ValidationResult(bool isSuccess, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Success() => new(true);
    public static ValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
