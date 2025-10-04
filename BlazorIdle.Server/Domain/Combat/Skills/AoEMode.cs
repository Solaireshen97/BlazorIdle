namespace BlazorIdle.Server.Domain.Combat.Skills;

public enum AoEMode
{
    None = 0,        // 单体（默认）
    CleaveFull = 1,  // 对每个目标造成完整伤害
    SplitEven = 2    // 将伤害在目标之间平均分摊（总伤不变）
}