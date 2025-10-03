namespace BlazorIdle.Server.Domain.Combat.Buffs;

public class BuffAggregate
{
    public double AdditiveHaste { get; set; }
    public double MultiplicativeHasteFactor { get; set; } = 1.0;

    public double ApplyToBaseHaste(double baseFactor)
    {
        // 假设 baseFactor 是原本的 HasteFactor（1.0 = 无加速）
        var result = (baseFactor + AdditiveHaste) * MultiplicativeHasteFactor;
        if (result < 0.1) result = 0.1;
        return result;
    }
}