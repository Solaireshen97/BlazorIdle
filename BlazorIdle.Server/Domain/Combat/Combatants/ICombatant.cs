using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat.Combatants;

/// <summary>
/// 战斗单位抽象接口（玩家和怪物的共同基础）
/// 未来可统一为 Actor，当前保持最小侵入
/// </summary>
public interface ICombatant
{
    /// <summary>
    /// 战斗单位唯一标识符
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 战斗单位名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 当前生命值
    /// </summary>
    int CurrentHp { get; }

    /// <summary>
    /// 最大生命值
    /// </summary>
    int MaxHp { get; }

    /// <summary>
    /// 是否已死亡
    /// </summary>
    bool IsDead { get; }

    /// <summary>
    /// 战斗状态
    /// </summary>
    CombatantState State { get; }

    /// <summary>
    /// 死亡时间（如果已死亡）
    /// </summary>
    double? DeathTime { get; }

    /// <summary>
    /// 复活时间（如果正在复活）
    /// </summary>
    double? ReviveAt { get; }

    /// <summary>
    /// 仇恨权重 - 影响被选中为目标的概率
    /// 默认 1.0，嘲讽可提高至 5.0+
    /// </summary>
    double ThreatWeight { get; set; }

    /// <summary>
    /// 接收伤害
    /// </summary>
    /// <param name="amount">伤害数值</param>
    /// <param name="type">伤害类型</param>
    /// <param name="now">当前游戏时间</param>
    /// <returns>实际造成的伤害</returns>
    int ReceiveDamage(int amount, DamageType type, double now);

    /// <summary>
    /// 是否可以被选择为攻击目标
    /// </summary>
    bool CanBeTargeted();

    /// <summary>
    /// 是否可以执行行动（攻击、施法等）
    /// </summary>
    bool CanAct();
}
