using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 格挡计算服务
/// 计算盾牌格挡概率和格挡减伤
/// </summary>
public class BlockCalculator
{
    // 格挡常数
    private const double BASE_BLOCK_CHANCE = 0.05;          // 基础格挡率 5%
    private const double BLOCK_DAMAGE_REDUCTION = 0.30;     // 格挡减伤 30%
    private const double BLOCK_CHANCE_PER_STRENGTH = 0.001; // 每点力量增加0.1%格挡率
    private const double BLOCK_CHANCE_PER_ITEMLEVEL = 0.002; // 每点物品等级增加0.2%格挡率
    private const double MAX_BLOCK_CHANCE = 0.50;           // 最大格挡率 50%

    /// <summary>
    /// 计算盾牌格挡率
    /// </summary>
    /// <param name="shieldItemLevel">盾牌物品等级</param>
    /// <param name="characterStrength">角色力量值</param>
    /// <returns>格挡率（0-0.5）</returns>
    public double CalculateBlockChance(int shieldItemLevel, double characterStrength = 0)
    {
        double blockChance = BASE_BLOCK_CHANCE;
        
        // 盾牌物品等级贡献
        blockChance += shieldItemLevel * BLOCK_CHANCE_PER_ITEMLEVEL;
        
        // 力量属性贡献
        blockChance += characterStrength * BLOCK_CHANCE_PER_STRENGTH;
        
        // 限制最大值
        return Math.Min(blockChance, MAX_BLOCK_CHANCE);
    }

    /// <summary>
    /// 计算格挡伤害减免
    /// </summary>
    /// <param name="incomingDamage">进入伤害</param>
    /// <returns>格挡后伤害</returns>
    public int ApplyBlockReduction(int incomingDamage)
    {
        return (int)(incomingDamage * (1.0 - BLOCK_DAMAGE_REDUCTION));
    }

    /// <summary>
    /// 判断是否格挡成功
    /// </summary>
    /// <param name="blockChance">格挡率</param>
    /// <param name="random">随机数生成器</param>
    /// <returns>是否格挡成功</returns>
    public bool RollBlock(double blockChance, Random? random = null)
    {
        random ??= new Random();
        return random.NextDouble() < blockChance;
    }

    /// <summary>
    /// 检查装备是否为盾牌
    /// </summary>
    public static bool IsShield(WeaponType weaponType)
    {
        return weaponType == WeaponType.Shield;
    }

    /// <summary>
    /// 获取格挡减伤百分比（供UI显示）
    /// </summary>
    public static double GetBlockDamageReduction()
    {
        return BLOCK_DAMAGE_REDUCTION;
    }

    /// <summary>
    /// 获取最大格挡率（供UI显示）
    /// </summary>
    public static double GetMaxBlockChance()
    {
        return MAX_BLOCK_CHANCE;
    }
    
    /// <summary>
    /// 格式化格挡率为百分比字符串（供UI显示）
    /// </summary>
    /// <param name="blockChance">格挡率（0-1）</param>
    /// <returns>格式化的百分比字符串，例如"25.5%"</returns>
    public static string FormatBlockChancePercentage(double blockChance)
    {
        return $"{blockChance * 100:F1}%";
    }
    
    /// <summary>
    /// 获取格挡信息摘要（供UI显示）
    /// </summary>
    /// <param name="blockChance">当前格挡率</param>
    /// <param name="shieldItemLevel">盾牌物品等级</param>
    /// <param name="characterStrength">角色力量值</param>
    /// <returns>格挡信息描述</returns>
    public static string GetBlockSummary(double blockChance, int shieldItemLevel, double characterStrength)
    {
        var percentage = FormatBlockChancePercentage(blockChance);
        var maxPercentage = FormatBlockChancePercentage(MAX_BLOCK_CHANCE);
        var reductionPercentage = FormatBlockChancePercentage(BLOCK_DAMAGE_REDUCTION);
        
        return $"格挡率: {percentage} (最大{maxPercentage}) | 格挡减伤: {reductionPercentage} | 盾牌等级: {shieldItemLevel} | 力量: {characterStrength:F0}";
    }
}
