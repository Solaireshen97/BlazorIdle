using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using Xunit;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 武器攻击速度集成测试
/// 验证不同武器类型的攻击速度和伤害倍率
/// </summary>
public class WeaponSpeedIntegrationTests
{
    [Theory]
    [InlineData(WeaponType.Dagger, 1.8)] // 匕首：快速
    [InlineData(WeaponType.Sword, 2.4)] // 单手剑：标准
    [InlineData(WeaponType.TwoHandSword, 3.4)] // 双手剑：慢速
    [InlineData(WeaponType.Staff, 3.0)] // 法杖：中等
    public void AttackSpeedCalculator_ShouldReturnCorrectBaseSpeed(WeaponType weaponType, double expectedSpeed)
    {
        // Arrange
        var calculator = new AttackSpeedCalculator();

        // Act
        var actualSpeed = calculator.GetBaseAttackSpeed(weaponType);

        // Assert
        Assert.Equal(expectedSpeed, actualSpeed, precision: 2);
    }

    [Fact]
    public void AttackSpeedCalculator_WithHaste_ShouldReduceAttackInterval()
    {
        // Arrange
        var calculator = new AttackSpeedCalculator();
        var weaponType = WeaponType.Sword; // 基础2.4秒

        // Act - 10%急速应该使攻击间隔变为 2.4 / 1.1 ≈ 2.18秒
        var speedWithHaste = calculator.CalculateAttackSpeed(weaponType, 0.10);

        // Assert
        Assert.True(speedWithHaste < 2.4, "急速应该降低攻击间隔");
        Assert.InRange(speedWithHaste, 2.15, 2.20); // 2.4 / 1.1 ≈ 2.18
    }

    [Fact]
    public void WeaponDamageCalculator_SingleWeapon_ShouldApplyDamageMultiplier()
    {
        // Arrange
        var attackSpeedCalc = new AttackSpeedCalculator();
        var weaponDamageCalc = new WeaponDamageCalculator(attackSpeedCalc);

        double baseDamage = 10.0;
        double attackPower = 50.0;

        // Act - 单手剑（速度2.4，系数0.42，倍率约1.0）
        var swordDamage = weaponDamageCalc.CalculateWeaponDamage(
            baseDamage, 
            attackPower, 
            WeaponType.Sword, 
            WeaponType.None, 
            isDualWielding: false
        );

        // Assert - 伤害应该在合理范围
        Assert.InRange(swordDamage, 50, 70); // (10 + 50) * 1.0 ≈ 60
    }

    [Fact]
    public void WeaponDamageCalculator_DualWield_ShouldProvideMoreDamage()
    {
        // Arrange
        var attackSpeedCalc = new AttackSpeedCalculator();
        var weaponDamageCalc = new WeaponDamageCalculator(attackSpeedCalc);

        double baseDamage = 10.0;
        double attackPower = 50.0;

        // Act - 单手剑 vs 双持双剑
        var singleSwordDamage = weaponDamageCalc.CalculateWeaponDamage(
            baseDamage, attackPower, WeaponType.Sword, WeaponType.None, isDualWielding: false
        );

        var dualSwordDamage = weaponDamageCalc.CalculateWeaponDamage(
            baseDamage, attackPower, WeaponType.Sword, WeaponType.Sword, isDualWielding: true
        );

        // Assert - 双持应该提供更高伤害
        Assert.True(dualSwordDamage > singleSwordDamage, 
            $"双持伤害({dualSwordDamage})应该大于单手({singleSwordDamage})");
        
        // 双持提升应该在合理范围（约70-90%提升，因为副手系数0.85）
        // 计算：主手 + 副手*0.85 = 1 + 0.85 = 1.85倍
        var damageIncrease = (dualSwordDamage - singleSwordDamage) / singleSwordDamage;
        Assert.InRange(damageIncrease, 0.70, 0.90);
    }

    [Fact]
    public void WeaponDamageCalculator_TwoHandWeapon_ShouldHaveHigherDPS()
    {
        // Arrange
        var attackSpeedCalc = new AttackSpeedCalculator();
        var weaponDamageCalc = new WeaponDamageCalculator(attackSpeedCalc);

        double baseDamage = 10.0;
        double attackPower = 50.0;

        // Act
        var swordDamage = weaponDamageCalc.CalculateWeaponDamage(
            baseDamage, attackPower, WeaponType.Sword, WeaponType.None, isDualWielding: false
        );

        var twoHandSwordDamage = weaponDamageCalc.CalculateWeaponDamage(
            baseDamage, attackPower, WeaponType.TwoHandSword, WeaponType.None, isDualWielding: false
        );

        // 双手武器伤害倍率更高（但攻击速度慢），单次伤害应该更高
        Assert.True(twoHandSwordDamage > swordDamage,
            $"双手剑单次伤害({twoHandSwordDamage})应该大于单手剑({swordDamage})");

        // 计算DPS（伤害/攻击间隔）
        var swordSpeed = attackSpeedCalc.GetBaseAttackSpeed(WeaponType.Sword);
        var twoHandSpeed = attackSpeedCalc.GetBaseAttackSpeed(WeaponType.TwoHandSword);

        var swordDPS = swordDamage / swordSpeed;
        var twoHandDPS = twoHandSwordDamage / twoHandSpeed;

        // 双手武器DPS应该略高（约20-30%）
        Assert.True(twoHandDPS > swordDPS * 1.15,
            $"双手剑DPS({twoHandDPS:F2})应该明显高于单手剑DPS({swordDPS:F2})");
    }

    [Fact]
    public void WeaponTypeClassification_ShouldBeCorrect()
    {
        // 双手武器
        Assert.True(AttackSpeedCalculator.IsTwoHandedWeapon(WeaponType.TwoHandSword));
        Assert.True(AttackSpeedCalculator.IsTwoHandedWeapon(WeaponType.Staff));
        Assert.True(AttackSpeedCalculator.IsTwoHandedWeapon(WeaponType.Bow));

        // 单手武器
        Assert.False(AttackSpeedCalculator.IsTwoHandedWeapon(WeaponType.Sword));
        Assert.False(AttackSpeedCalculator.IsTwoHandedWeapon(WeaponType.Dagger));

        // 可双持武器
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Sword));
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Dagger));
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Axe));

        // 不可双持武器
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.TwoHandSword));
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.Shield));
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.Wand));
    }

    [Fact]
    public void DualWieldHitPenalty_ShouldBeReasonable()
    {
        // Arrange
        var weaponDamageCalc = new WeaponDamageCalculator(new AttackSpeedCalculator());

        // Act
        var hitChanceNotDualWielding = weaponDamageCalc.GetOffHandHitChance(isDualWielding: false);
        var hitChanceDualWielding = weaponDamageCalc.GetOffHandHitChance(isDualWielding: true);

        // Assert
        Assert.Equal(1.0, hitChanceNotDualWielding); // 非双持100%命中
        Assert.InRange(hitChanceDualWielding, 0.75, 0.85); // 双持约81%命中（19%惩罚）
    }
}
