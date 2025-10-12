using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 武器伤害计算服务
/// 负责计算基于武器的攻击伤害，包括双持机制
/// </summary>
public class WeaponDamageCalculator
{
    private readonly AttackSpeedCalculator _attackSpeedCalculator;

    public WeaponDamageCalculator(AttackSpeedCalculator attackSpeedCalculator)
    {
        _attackSpeedCalculator = attackSpeedCalculator;
    }

    /// <summary>
    /// 计算武器攻击伤害
    /// </summary>
    /// <param name="baseDamage">基础伤害值</param>
    /// <param name="attackPower">攻击强度</param>
    /// <param name="mainHandWeapon">主手武器类型</param>
    /// <param name="offHandWeapon">副手武器类型</param>
    /// <param name="isDualWielding">是否双持</param>
    /// <returns>计算后的伤害值</returns>
    public double CalculateWeaponDamage(
        double baseDamage,
        double attackPower,
        WeaponType mainHandWeapon,
        WeaponType offHandWeapon,
        bool isDualWielding)
    {
        // 基础伤害 = 基础值 + 攻击强度
        double totalDamage = baseDamage + attackPower;

        if (!isDualWielding)
        {
            // 单手或双手武器：应用主手武器伤害倍率
            double weaponMultiplier = _attackSpeedCalculator.CalculateWeaponDamageMultiplier(mainHandWeapon);
            return totalDamage * weaponMultiplier;
        }
        else
        {
            // 双持：主手全额伤害 + 副手伤害（有惩罚）
            double mainHandMultiplier = _attackSpeedCalculator.CalculateWeaponDamageMultiplier(mainHandWeapon);
            double offHandMultiplier = _attackSpeedCalculator.CalculateWeaponDamageMultiplier(offHandWeapon);
            
            // 副手伤害系数 = 0.85（副手伤害稍低）
            const double offHandDamageCoefficient = 0.85;
            
            // 双持总伤害 = 主手伤害 + 副手伤害 * 0.85
            double mainHandDamage = totalDamage * mainHandMultiplier;
            double offHandDamage = totalDamage * offHandMultiplier * offHandDamageCoefficient;
            
            return mainHandDamage + offHandDamage;
        }
    }

    /// <summary>
    /// 计算副手武器命中率
    /// 双持时副手有19%未命中惩罚
    /// </summary>
    /// <param name="isDualWielding">是否双持</param>
    /// <returns>副手命中率（0-1），单手武器返回1.0</returns>
    public double GetOffHandHitChance(bool isDualWielding)
    {
        if (!isDualWielding)
        {
            return 1.0;
        }

        // 双持副手命中率 = 1 - 惩罚
        return 1.0 - AttackSpeedCalculator.GetDualWieldHitPenalty();
    }

    /// <summary>
    /// 获取武器类型显示信息
    /// </summary>
    public string GetWeaponDisplayInfo(WeaponType mainHand, WeaponType offHand, bool isDualWielding)
    {
        if (isDualWielding)
        {
            return $"双持: {AttackSpeedCalculator.GetWeaponTypeName(mainHand)} + {AttackSpeedCalculator.GetWeaponTypeName(offHand)}";
        }
        else if (mainHand != WeaponType.None)
        {
            if (AttackSpeedCalculator.IsTwoHandedWeapon(mainHand))
            {
                return $"双手武器: {AttackSpeedCalculator.GetWeaponTypeName(mainHand)}";
            }
            return $"单手武器: {AttackSpeedCalculator.GetWeaponTypeName(mainHand)}";
        }
        return "空手";
    }
}
