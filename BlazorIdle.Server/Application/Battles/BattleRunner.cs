using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Application.Battles;

/// <summary>
/// 同步版战斗驱动：基于通用 BattleEngine 一次性推进到目标时长并返回段聚合。
/// </summary>
public class BattleRunner
{
    public IReadOnlyList<CombatSegment> RunForDuration(
        Battle battle,
        double durationSeconds,
        Profession profession,
        RngContext rng,
        out bool killed,
        out double? killTime,
        out int overkill,
        IProfessionModule? module = null,
        Encounter? encounter = null,                  // 保留签名兼容（内部改为 group）
        EncounterGroup? encounterGroup = null,        // 保留签名兼容
        CharacterStats? stats = null)
    {
        // 组装引擎（EncounterGroup 由 Engine 内部创建，这里传入敌人定义与数量）
        var enemyDef = (encounterGroup?.All?.FirstOrDefault()?.Enemy)
                       ?? (encounter is not null ? encounter.Enemy : EnemyRegistry.Resolve("dummy"));
        var enemyCount = encounterGroup?.All?.Count ?? (encounter is null ? 1 : 1);

        var engine = new BattleEngine(
            battleId: battle.Id,
            characterId: battle.CharacterId,
            profession: profession,
            stats: stats ?? new CharacterStats(),
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: enemyCount,
            module: module
        );

        // 一次性推进
        engine.AdvanceUntil(durationSeconds);

        killed = engine.Killed;
        killTime = engine.KillTime;
        overkill = engine.Overkill;

        // 将结束时间/间隔回写（保持原行为）
        battle.Finish(engine.Battle.EndedAt ?? engine.Clock.CurrentTime);
        return engine.Segments;
    }
}