using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 格挡计算服务
/// 计算盾牌格挡概率和格挡减伤
/// </summary>
public class BlockCalculator
{
    private readonly EquipmentSystemOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">装备系统配置选项</param>
    public BlockCalculator(IOptions<EquipmentSystemOptions>? options = null)
    {
        _options = options?.Value ?? new EquipmentSystemOptions();
    }

    /// <summary>
    /// 计算盾牌格挡率
    /// </summary>
    /// <param name="shieldItemLevel">盾牌物品等级</param>
    /// <param name="characterStrength">角色力量值</param>
    /// <returns>格挡率（0-0.5）</returns>
    public double CalculateBlockChance(int shieldItemLevel, double characterStrength = 0)
    {
        double blockChance = _options.BlockCalculation.BaseBlockChance;
        
        // 盾牌物品等级贡献
        blockChance += shieldItemLevel * _options.BlockCalculation.BlockChancePerItemLevel;
        
        // 力量属性贡献
        blockChance += characterStrength * _options.BlockCalculation.BlockChancePerStrength;
        
        // 限制最大值
        return Math.Min(blockChance, _options.BlockCalculation.MaxBlockChance);
    }

    /// <summary>
    /// 计算格挡伤害减免
    /// </summary>
    /// <param name="incomingDamage">进入伤害</param>
    /// <returns>格挡后伤害</returns>
    public int ApplyBlockReduction(int incomingDamage)
    {
        return (int)(incomingDamage * (1.0 - _options.BlockCalculation.BlockDamageReduction));
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
    public double GetBlockDamageReduction()
    {
        return _options.BlockCalculation.BlockDamageReduction;
    }

    /// <summary>
    /// 获取最大格挡率（供UI显示）
    /// </summary>
    public double GetMaxBlockChance()
    {
        return _options.BlockCalculation.MaxBlockChance;
    }
}
