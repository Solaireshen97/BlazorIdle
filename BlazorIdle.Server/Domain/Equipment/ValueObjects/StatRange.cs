namespace BlazorIdle.Server.Domain.Equipment.ValueObjects;

/// <summary>
/// 属性范围值对象
/// </summary>
public class StatRange
{
    /// <summary>
    /// 最小值
    /// </summary>
    public double Min { get; set; }
    
    /// <summary>
    /// 最大值
    /// </summary>
    public double Max { get; set; }
    
    public StatRange()
    {
    }
    
    public StatRange(double min, double max)
    {
        Min = min;
        Max = max;
    }
    
    /// <summary>
    /// 在范围内随机Roll一个值
    /// </summary>
    public double Roll(Random random)
    {
        return Min + (Max - Min) * random.NextDouble();
    }
}
