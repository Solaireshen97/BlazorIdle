namespace BlazorIdle.Server.Domain.Characters;

public sealed class CharacterStats
{
    // 进攻面板（Phase 1）
    public double AttackPower { get; init; } = 0;
    public double SpellPower { get; init; } = 0;

    // 暴击（作为基础值，技能/BUFF 可覆盖/叠加）
    // CritChance: 0..1; CritMultiplier: >= 1（例如 2.0 = 200%）
    public double CritChance { get; init; } = 0.0;
    public double CritMultiplier { get; init; } = 2.0;

    // 急速（基础值，最终会与 BuffAggregate 的乘因子叠乘）
    // 例如 0.20 表示 +20% 基础急速
    public double HastePercent { get; init; } = 0.0;

    // 穿透（与 BuffAggregate 的穿透叠加后参与结算）
    public double ArmorPenFlat { get; init; } = 0.0;
    public double ArmorPenPct { get; init; } = 0.0; // 0..1
    public double MagicPenFlat { get; init; } = 0.0;
    public double MagicPenPct { get; init; } = 0.0; // 0..1

    // 防御面板（Phase 4）
    /// <summary>
    /// 护甲值 - 用于物理伤害减免计算
    /// </summary>
    public double Armor { get; init; } = 0.0;
}