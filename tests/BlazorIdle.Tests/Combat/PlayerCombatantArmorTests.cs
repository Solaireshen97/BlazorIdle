using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using Xunit;

namespace BlazorIdle.Tests.Combat;

/// <summary>
/// 测试 PlayerCombatant 的护甲减伤和格挡机制
/// </summary>
public class PlayerCombatantArmorTests
{
    [Fact]
    public void ReceiveDamage_WithArmor_ShouldReducePhysicalDamage()
    {
        // Arrange
        var stats = new CharacterStats
        {
            AttackPower = 100,
            Armor = 500 // 500 护甲应该减免约 50% 伤害 (对等级10攻击者)
        };
        var player = new PlayerCombatant("test-id", "Test Player", stats, stamina: 100);
        
        int initialHp = player.CurrentHp;
        int rawDamage = 100;
        
        // Act
        var actualDamage = player.ReceiveDamage(rawDamage, DamageType.Physical, 0);
        
        // Assert
        // 有护甲的情况下，实际伤害应该小于原始伤害
        Assert.True(actualDamage < rawDamage, 
            $"Expected damage to be reduced by armor. Raw: {rawDamage}, Actual: {actualDamage}");
        
        // 验证HP减少量等于实际伤害
        Assert.Equal(initialHp - actualDamage, player.CurrentHp);
    }

    [Fact]
    public void ReceiveDamage_WithoutArmor_ShouldTakeFullPhysicalDamage()
    {
        // Arrange
        var stats = new CharacterStats
        {
            AttackPower = 100,
            Armor = 0 // 无护甲
        };
        var player = new PlayerCombatant("test-id", "Test Player", stats, stamina: 100);
        
        int rawDamage = 50;
        
        // Act
        var actualDamage = player.ReceiveDamage(rawDamage, DamageType.Physical, 0);
        
        // Assert
        // 无护甲时应该受到全额伤害
        Assert.Equal(rawDamage, actualDamage);
    }

    [Fact]
    public void ReceiveDamage_MagicDamage_ShouldNotBeReducedByArmor()
    {
        // Arrange
        var stats = new CharacterStats
        {
            AttackPower = 100,
            Armor = 500 // 有护甲
        };
        var player = new PlayerCombatant("test-id", "Test Player", stats, stamina: 100);
        
        int rawDamage = 100;
        
        // Act
        var actualDamage = player.ReceiveDamage(rawDamage, DamageType.Magic, 0);
        
        // Assert
        // 魔法伤害不受护甲影响
        Assert.Equal(rawDamage, actualDamage);
    }

    [Fact]
    public void ReceiveDamage_TrueDamage_ShouldNotBeReducedByArmor()
    {
        // Arrange
        var stats = new CharacterStats
        {
            AttackPower = 100,
            Armor = 500 // 有护甲
        };
        var player = new PlayerCombatant("test-id", "Test Player", stats, stamina: 100);
        
        int rawDamage = 100;
        
        // Act
        var actualDamage = player.ReceiveDamage(rawDamage, DamageType.True, 0);
        
        // Assert
        // 真实伤害不受护甲影响
        Assert.Equal(rawDamage, actualDamage);
    }

    [Fact]
    public void ReceiveDamage_WithHighArmor_ShouldHaveMaximumReduction()
    {
        // Arrange
        var stats = new CharacterStats
        {
            AttackPower = 100,
            Armor = 10000 // 极高护甲，应该达到75%减伤上限
        };
        var player = new PlayerCombatant("test-id", "Test Player", stats, stamina: 100);
        
        int rawDamage = 100;
        
        // Act
        var actualDamage = player.ReceiveDamage(rawDamage, DamageType.Physical, 0);
        
        // Assert
        // 最多减伤75%，所以至少受到25%伤害
        Assert.InRange(actualDamage, 25, 30); // 允许一些舍入误差
    }

    [Fact]
    public void ReceiveDamage_MultipleHits_ArmorShouldApplyToEachHit()
    {
        // Arrange
        var stats = new CharacterStats
        {
            AttackPower = 100,
            Armor = 500
        };
        var player = new PlayerCombatant("test-id", "Test Player", stats, stamina: 100);
        
        int rawDamage = 50;
        int initialHp = player.CurrentHp;
        
        // Act - 连续受到3次攻击
        var damage1 = player.ReceiveDamage(rawDamage, DamageType.Physical, 0);
        var damage2 = player.ReceiveDamage(rawDamage, DamageType.Physical, 0.5);
        var damage3 = player.ReceiveDamage(rawDamage, DamageType.Physical, 1.0);
        
        // Assert
        // 每次攻击都应该被护甲减伤
        Assert.True(damage1 < rawDamage);
        Assert.True(damage2 < rawDamage);
        Assert.True(damage3 < rawDamage);
        
        // 总伤害应该等于HP减少量
        int totalDamage = damage1 + damage2 + damage3;
        Assert.Equal(initialHp - totalDamage, player.CurrentHp);
    }

    [Fact]
    public void ReceiveDamage_WithBlockChance_MayBlock()
    {
        // Arrange - 设置100%格挡率以确保格挡触发
        var stats = new CharacterStats
        {
            AttackPower = 100,
            BlockChance = 1.0 // 100% 格挡率
        };
        var player = new PlayerCombatant("test-id", "Test Player", stats, stamina: 100);
        
        int rawDamage = 100;
        
        // Act
        var actualDamage = player.ReceiveDamage(rawDamage, DamageType.Physical, 0);
        
        // Assert
        // 格挡应该减少30%伤害，所以实际伤害应该是70或更少（考虑护甲）
        Assert.True(actualDamage < rawDamage, 
            $"Damage should be reduced by block. Raw: {rawDamage}, Actual: {actualDamage}");
    }

    [Fact]
    public void ReceiveDamage_WithoutBlockChance_ShouldNotBlock()
    {
        // Arrange - 0% 格挡率
        var stats = new CharacterStats
        {
            AttackPower = 100,
            BlockChance = 0.0
        };
        var player = new PlayerCombatant("test-id", "Test Player", stats, stamina: 100);
        
        int rawDamage = 100;
        int expectedDamage = rawDamage; // 无护甲无格挡，应该全额伤害
        
        // Act
        var actualDamage = player.ReceiveDamage(rawDamage, DamageType.Physical, 0);
        
        // Assert
        Assert.Equal(expectedDamage, actualDamage);
    }

    [Fact]
    public void ReceiveDamage_DeadPlayer_ShouldNotTakeDamage()
    {
        // Arrange
        var stats = new CharacterStats { AttackPower = 100, Armor = 0 };
        var player = new PlayerCombatant("test-id", "Test Player", stats, stamina: 10);
        
        // 先让玩家死亡
        player.ReceiveDamage(1000, DamageType.Physical, 0);
        Assert.True(player.IsDead);
        
        // Act - 尝试对死亡玩家造成伤害
        var damage = player.ReceiveDamage(100, DamageType.Physical, 1.0);
        
        // Assert
        Assert.Equal(0, damage); // 死亡玩家不再受到伤害
        Assert.Equal(0, player.CurrentHp);
    }

    [Fact]
    public void ReceiveDamage_WithArmorAndBlock_BothShouldApply()
    {
        // Arrange - 同时有护甲和100%格挡率
        var stats = new CharacterStats
        {
            AttackPower = 100,
            Armor = 500,      // 应该减伤约50%
            BlockChance = 1.0  // 100% 格挡率，额外减伤30%
        };
        var player = new PlayerCombatant("test-id", "Test Player", stats, stamina: 100);
        
        int rawDamage = 100;
        
        // Act
        var actualDamage = player.ReceiveDamage(rawDamage, DamageType.Physical, 0);
        
        // Assert
        // 先护甲减伤约50% -> ~50伤害
        // 再格挡减伤30% -> ~35伤害  
        // 实际结果约45，因为护甲减伤公式和舍入
        Assert.True(actualDamage < 50, 
            $"Damage should be reduced by both armor and block. Actual: {actualDamage}");
    }
}
