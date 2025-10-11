using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Equipment.Services;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 护甲减伤集成测试
/// 验证玩家受到攻击时护甲减伤和格挡机制正常工作
/// </summary>
public class ArmorMitigationIntegrationTests
{
    [Fact]
    public void PlayerWithArmor_ShouldTakeLessDamage()
    {
        // Arrange
        var armorCalculator = new ArmorCalculator();
        var baseStats = new CharacterStats
        {
            AttackPower = 50.0,
            SpellPower = 0.0,
            CritChance = 0.05,
            CritMultiplier = 2.0,
            HastePercent = 0.0,
            ArmorPenFlat = 0.0,
            ArmorPenPct = 0.0,
            MagicPenFlat = 0.0,
            MagicPenPct = 0.0
        };

        // 创建两个角色：一个有护甲，一个没有
        var playerWithoutArmor = new PlayerCombatant(
            "player1", 
            "无甲战士", 
            baseStats, 
            stamina: 10
        );

        var playerWithArmor = new PlayerCombatant(
            "player2", 
            "板甲战士", 
            baseStats, 
            stamina: 10, 
            armorCalculator: armorCalculator
        );
        playerWithArmor.TotalArmor = 1000; // 1000护甲，在50级时约33%减伤

        // Act - 模拟敌人攻击
        int baseDamage = 100;
        var damage1 = playerWithoutArmor.ReceiveDamage(baseDamage, DamageType.Physical, 0.0);
        var damage2 = playerWithArmor.ReceiveDamage(baseDamage, DamageType.Physical, 0.0);

        // Assert
        Assert.Equal(100, damage1); // 无护甲角色受到全额伤害
        Assert.True(damage2 < damage1, $"有护甲的角色应该受到更少伤害。无护甲: {damage1}, 有护甲: {damage2}");
        
        // 验证减伤百分比合理（1000护甲在50级约25%减伤，即受到75伤害）
        // 公式: 1000 / (1000 + 50*50 + 400) = 1000 / 3900 ≈ 25.6%
        var reductionPercent = (damage1 - damage2) / (double)damage1;
        Assert.True(reductionPercent > 0.20 && reductionPercent < 0.30, 
            $"1000护甲应该提供约25%减伤，实际减伤: {reductionPercent:P2}");
    }

    [Fact]
    public void PlayerWithShield_CanBlockDamage()
    {
        // Arrange
        var blockCalculator = new BlockCalculator();
        var baseStats = new CharacterStats
        {
            AttackPower = 50.0,
            SpellPower = 0.0,
            CritChance = 0.05,
            CritMultiplier = 2.0,
            HastePercent = 0.0,
            ArmorPenFlat = 0.0,
            ArmorPenPct = 0.0,
            MagicPenFlat = 0.0,
            MagicPenPct = 0.0
        };

        // 创建装备盾牌的角色（100%格挡率用于测试）
        var playerWithShield = new PlayerCombatant(
            "player1", 
            "盾牌战士", 
            baseStats, 
            stamina: 10,
            blockCalculator: blockCalculator
        );
        playerWithShield.BlockChance = 1.0; // 100%格挡率（测试用）

        // Act
        int baseDamage = 100;
        var damageReceived = playerWithShield.ReceiveDamage(baseDamage, DamageType.Physical, 0.0);

        // Assert - 格挡应该减少30%伤害
        var expectedDamage = (int)(100 * 0.7); // 30%减伤
        Assert.Equal(expectedDamage, damageReceived);
    }

    [Fact]
    public void PlayerWithArmorAndShield_ShouldStackMitigation()
    {
        // Arrange
        var armorCalculator = new ArmorCalculator();
        var blockCalculator = new BlockCalculator();
        var baseStats = new CharacterStats
        {
            AttackPower = 50.0,
            SpellPower = 0.0,
            CritChance = 0.05,
            CritMultiplier = 2.0,
            HastePercent = 0.0,
            ArmorPenFlat = 0.0,
            ArmorPenPct = 0.0,
            MagicPenFlat = 0.0,
            MagicPenPct = 0.0
        };

        // 创建同时有护甲和盾牌的角色
        var tankyPlayer = new PlayerCombatant(
            "player1", 
            "重甲坦克", 
            baseStats, 
            stamina: 10,
            armorCalculator: armorCalculator,
            blockCalculator: blockCalculator
        );
        tankyPlayer.TotalArmor = 1000; // 约33%护甲减伤
        tankyPlayer.BlockChance = 1.0; // 100%格挡率（测试用）

        // Act
        int baseDamage = 100;
        var damageReceived = tankyPlayer.ReceiveDamage(baseDamage, DamageType.Physical, 0.0);

        // Assert - 应该先格挡30%，然后护甲再减伤约25%
        // 100 -> 70 (格挡) -> ~53 (护甲: 70 * 0.744 ≈ 52-53)
        Assert.True(damageReceived < 55, 
            $"护甲和格挡叠加应该大幅减少伤害。实际受到: {damageReceived}");
        Assert.True(damageReceived > 50,
            $"减伤应该在合理范围。实际受到: {damageReceived}");
    }

    [Fact]
    public void MagicDamage_ShouldIgnoreArmorAndBlock()
    {
        // Arrange
        var armorCalculator = new ArmorCalculator();
        var blockCalculator = new BlockCalculator();
        var baseStats = new CharacterStats
        {
            AttackPower = 50.0,
            SpellPower = 0.0,
            CritChance = 0.05,
            CritMultiplier = 2.0,
            HastePercent = 0.0,
            ArmorPenFlat = 0.0,
            ArmorPenPct = 0.0,
            MagicPenFlat = 0.0,
            MagicPenPct = 0.0
        };

        var player = new PlayerCombatant(
            "player1", 
            "法师", 
            baseStats, 
            stamina: 10,
            armorCalculator: armorCalculator,
            blockCalculator: blockCalculator
        );
        player.TotalArmor = 1000;
        player.BlockChance = 1.0;

        // Act - 魔法伤害
        int baseDamage = 100;
        var damageReceived = player.ReceiveDamage(baseDamage, DamageType.Magic, 0.0);

        // Assert - 魔法伤害应该无视护甲和格挡
        Assert.Equal(100, damageReceived);
    }
}
