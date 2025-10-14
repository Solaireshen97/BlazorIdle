using BlazorIdle.Server.Config;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Services;

/// <summary>
/// 战斗消息格式化服务
/// 根据配置的消息模板生成格式化的战斗事件消息
/// </summary>
public sealed class BattleMessageFormatter
{
    private readonly BattleMessageOptions _options;

    public BattleMessageFormatter(IOptions<BattleMessageOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// 格式化攻击开始消息
    /// </summary>
    public string FormatAttackStarted(string attackerName, string targetName)
    {
        return _options.AttackStartedTemplate
            .Replace("{attacker}", attackerName)
            .Replace("{target}", targetName);
    }

    /// <summary>
    /// 格式化伤害造成消息
    /// </summary>
    public string FormatDamageDealt(string attackerName, string targetName, int damage, bool isCrit)
    {
        var critSuffix = isCrit ? _options.CritSuffix : "";
        return _options.DamageDealtTemplate
            .Replace("{attacker}", attackerName)
            .Replace("{target}", targetName)
            .Replace("{damage}", damage.ToString())
            .Replace("{critSuffix}", critSuffix);
    }

    /// <summary>
    /// 格式化伤害接收消息
    /// </summary>
    public string FormatDamageReceived(string targetName, string attackerName, int damage)
    {
        return _options.DamageReceivedTemplate
            .Replace("{target}", targetName)
            .Replace("{attacker}", attackerName)
            .Replace("{damage}", damage.ToString());
    }

    /// <summary>
    /// 格式化敌人攻击开始消息
    /// </summary>
    public string FormatEnemyAttackStarted(string attackerName, string targetName)
    {
        return _options.EnemyAttackStartedTemplate
            .Replace("{attacker}", attackerName)
            .Replace("{target}", targetName);
    }

    /// <summary>
    /// 检查攻击开始事件是否启用
    /// </summary>
    public bool IsAttackStartedEnabled => _options.EnableAttackStartedEvent;

    /// <summary>
    /// 检查伤害造成事件是否启用
    /// </summary>
    public bool IsDamageDealtEnabled => _options.EnableDamageDealtEvent;

    /// <summary>
    /// 检查伤害接收事件是否启用
    /// </summary>
    public bool IsDamageReceivedEnabled => _options.EnableDamageReceivedEvent;

    /// <summary>
    /// 检查敌人攻击开始事件是否启用
    /// </summary>
    public bool IsEnemyAttackStartedEnabled => _options.EnableEnemyAttackStartedEvent;

    /// <summary>
    /// 获取玩家名称
    /// </summary>
    public string GetPlayerName() => _options.PlayerName;
}
