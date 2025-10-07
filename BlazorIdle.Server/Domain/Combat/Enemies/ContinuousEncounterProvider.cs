using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public sealed class ContinuousEncounterProvider : IEncounterProvider
{
    private readonly EnemyDefinition _enemy;
    private readonly int _count;
    private readonly double _respawnDelaySeconds;
    private readonly int? _initialEnemyHp;
    private bool _isFirstGroup = true;

    public EncounterGroup CurrentGroup { get; private set; }
    public int CurrentWaveIndex { get; private set; } = 1;
    public int CompletedRunCount { get; private set; } = 0;

    // respawnDelaySeconds: 击杀后刷新等待，默认 3s
    // initialEnemyHp: 第一个敌人的初始血量（用于战斗进度继承）
    public ContinuousEncounterProvider(EnemyDefinition enemy, int count, double respawnDelaySeconds = 3.0, int? initialEnemyHp = null)
    {
        _enemy = enemy;
        _count = count <= 0 ? 1 : count;
        _respawnDelaySeconds = respawnDelaySeconds <= 0 ? 0 : respawnDelaySeconds;
        _initialEnemyHp = initialEnemyHp;
        CurrentGroup = BuildGroup();
    }

    private EncounterGroup BuildGroup()
    {
        // 如果是第一个组且设置了初始血量，使用自定义血量
        if (_isFirstGroup && _initialEnemyHp.HasValue)
        {
            var encounters = new List<Encounter>();
            // 第一个敌人使用自定义血量
            encounters.Add(new Encounter(_enemy, _initialEnemyHp.Value));
            // 其余敌人使用满血
            for (int i = 1; i < _count; i++)
            {
                encounters.Add(new Encounter(_enemy));
            }
            return new EncounterGroup(encounters);
        }
        
        // 普通情况：所有敌人满血
        var defs = new List<EnemyDefinition>();
        for (int i = 0; i < _count; i++) defs.Add(_enemy);
        return new EncounterGroup(defs);
    }

    public bool TryAdvance(out EncounterGroup? nextGroup, out bool runCompleted)
    {
        // 持续模式：永远重生同配置
        _isFirstGroup = false; // 后续组都是满血
        nextGroup = BuildGroup();
        runCompleted = false;
        CurrentGroup = nextGroup;
        return true;
    }

    public double GetRespawnDelaySeconds(bool runJustCompleted) => _respawnDelaySeconds;
}