using BlazorIdle.Server.Config;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Shared.Models;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Services;

/// <summary>
/// 战斗事件消息格式化服务
/// 根据配置模板生成战斗事件消息
/// </summary>
public sealed class BattleEventMessageFormatter
{
    private readonly BattleEventsOptions _options;
    
    public BattleEventMessageFormatter(IOptions<BattleEventsOptions> options)
    {
        _options = options.Value;
    }
    
    /// <summary>
    /// 创建攻击开始事件
    /// </summary>
    public AttackStartEventDto CreateAttackStartEvent(
        Guid battleId,
        double eventTime,
        string attackerName,
        string attackerType,
        string targetName,
        string targetType,
        string attackType)
    {
        return new AttackStartEventDto
        {
            BattleId = battleId,
            EventTime = eventTime,
            EventType = "AttackStart",
            AttackerName = attackerName,
            AttackerType = attackerType,
            TargetName = targetName,
            TargetType = targetType,
            AttackType = attackType
        };
    }
    
    /// <summary>
    /// 创建伤害造成事件
    /// </summary>
    public DamageDealtEventDto CreateDamageDealtEvent(
        Guid battleId,
        double eventTime,
        string attackerName,
        string targetName,
        int damage,
        bool isCrit,
        DamageType damageType,
        int targetCurrentHp,
        int targetMaxHp)
    {
        return new DamageDealtEventDto
        {
            BattleId = battleId,
            EventTime = eventTime,
            EventType = "DamageDealt",
            AttackerName = attackerName,
            TargetName = targetName,
            Damage = damage,
            IsCrit = isCrit,
            DamageType = damageType.ToString(),
            TargetCurrentHp = targetCurrentHp,
            TargetMaxHp = targetMaxHp
        };
    }
    
    /// <summary>
    /// 创建受到伤害事件
    /// </summary>
    public DamageReceivedEventDto CreateDamageReceivedEvent(
        Guid battleId,
        double eventTime,
        string receiverName,
        string attackerName,
        int damage,
        DamageType damageType,
        int currentHp,
        int maxHp)
    {
        return new DamageReceivedEventDto
        {
            BattleId = battleId,
            EventTime = eventTime,
            EventType = "DamageReceived",
            ReceiverName = receiverName,
            AttackerName = attackerName,
            Damage = damage,
            DamageType = damageType.ToString(),
            CurrentHp = currentHp,
            MaxHp = maxHp
        };
    }
    
    /// <summary>
    /// 格式化攻击开始消息
    /// </summary>
    public string FormatAttackStartMessage(AttackStartEventDto evt)
    {
        if (!_options.EnableBattleEventMessages || !_options.Messages.AttackStart.Enabled)
            return string.Empty;
            
        string template = evt.AttackerType == "Player" 
            ? _options.Messages.AttackStart.PlayerAttacksEnemy
            : _options.Messages.AttackStart.EnemyAttacksPlayer;
            
        return template
            .Replace("{attacker}", evt.AttackerName)
            .Replace("{target}", evt.TargetName);
    }
    
    /// <summary>
    /// 格式化伤害造成消息
    /// </summary>
    public string FormatDamageDealtMessage(DamageDealtEventDto evt)
    {
        if (!_options.EnableBattleEventMessages || !_options.Messages.DamageDealt.Enabled)
            return string.Empty;
            
        string template = evt.IsCrit 
            ? _options.Messages.DamageDealt.Critical
            : _options.Messages.DamageDealt.Normal;
            
        string damageTypeName = _options.DamageTypeNames.TryGetValue(evt.DamageType, out var name) 
            ? name 
            : evt.DamageType;
            
        return template
            .Replace("{attacker}", evt.AttackerName)
            .Replace("{target}", evt.TargetName)
            .Replace("{damage}", evt.Damage.ToString())
            .Replace("{damageType}", damageTypeName);
    }
    
    /// <summary>
    /// 格式化受到伤害消息
    /// </summary>
    public string FormatDamageReceivedMessage(DamageReceivedEventDto evt, bool isPlayer)
    {
        if (!_options.EnableBattleEventMessages || !_options.Messages.DamageReceived.Enabled)
            return string.Empty;
            
        string template = isPlayer 
            ? _options.Messages.DamageReceived.Player
            : _options.Messages.DamageReceived.Enemy;
            
        string damageTypeName = _options.DamageTypeNames.TryGetValue(evt.DamageType, out var name) 
            ? name 
            : evt.DamageType;
            
        return template
            .Replace("{receiver}", evt.ReceiverName)
            .Replace("{attacker}", evt.AttackerName)
            .Replace("{damage}", evt.Damage.ToString())
            .Replace("{damageType}", damageTypeName)
            .Replace("{currentHp}", evt.CurrentHp.ToString())
            .Replace("{maxHp}", evt.MaxHp.ToString());
    }
}
