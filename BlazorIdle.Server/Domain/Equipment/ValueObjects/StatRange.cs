namespace BlazorIdle.Server.Domain.Equipment.ValueObjects;

/// <summary>
/// 属性范围值对象 - 用于定义装备生成时的属性范围
/// </summary>
public class StatRange
{
    /// <summary>最小值</summary>
    public double Min { get; set; }
    
    /// <summary>最大值</summary>
    public double Max { get; set; }
    
    public StatRange() { }
    
    public StatRange(double min, double max)
    {
        Min = min;
        Max = max;
    }
}
