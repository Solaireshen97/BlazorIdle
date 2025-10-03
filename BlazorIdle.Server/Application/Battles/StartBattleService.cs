using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Records;
using System.Text.Json;

namespace BlazorIdle.Server.Application.Battles;

/// <summary>
/// 用例服务：发起一次“离线同步模拟”战斗，并把模拟结果转换为可持久化的 BattleRecord + Segments。
/// 关键特征：
///  1. 不做实时推进，调用后一次性完成整段模拟。
///  2. 依赖仓储抽象（ICharacterRepository / IBattleRepository）隔离持久化细节。
///  3. 使用 BattleRunner 执行事件驱动模拟，得到内存中的 CombatSegment 列表。
///  4. 领域对象 (Battle) 和 持久化记录 (BattleRecord) 分离：前者用逻辑时间(double)，后者用墙钟(DateTime)。
/// </summary>
public class StartBattleService
{
    private readonly ICharacterRepository _characters; // 角色读取仓储
    private readonly IBattleRepository _battles;        // 战斗保存 / 读取仓储
    private readonly BattleRunner _runner;             // 战斗模拟器（纯内存事件调度）

    public StartBattleService(ICharacterRepository characters, IBattleRepository battles, BattleRunner runner)
    {
        _characters = characters;
        _battles = battles;
        _runner = runner;
    }

    /// <summary>
    /// 发起一次战斗模拟：
    ///  1. 校验角色存在
    ///  2. 构造 Battle 领域对象
    ///  3. 调用 BattleRunner 执行 durationSeconds 模拟
    ///  4. 汇总伤害 / 转换为 BattleRecord + 子段记录
    ///  5. 持久化
    ///  6. 返回战斗 Id
    /// </summary>
    /// <param name="characterId">角色 Id</param>
    /// <param name="simulateSeconds">模拟逻辑时长（秒）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>持久化后的战斗 Id（与 Battle.Id 一致）</returns>
    public async Task<Guid> StartAsync(Guid characterId, double simulateSeconds = 15, CancellationToken ct = default)
    {
        // 1. 读取角色（仅确认存在；未做额外状态/等级校验）
        var c = await _characters.GetAsync(characterId, ct);
        if (c is null) throw new InvalidOperationException("Character not found");

        // 2. 构造领域层 Battle （逻辑时间：StartedAt = 0；AttackIntervalSeconds 暂写死）
        var battleDomain = new Battle
        {
            CharacterId = characterId,
            AttackIntervalSeconds = 1.5, // TODO: 可改为根据角色属性或装备计算
            StartedAt = 0                 // 逻辑时钟起点（与 DateTime 脱钩）
        };

        // 3. 执行模拟（同步完成；内部推进事件调度直到达到 simulateSeconds）
        var segments = _runner.RunForDuration(battleDomain, simulateSeconds);

        // 4. 汇总总伤害（段内 TotalDamage 累加；假设 Runner 已保证一致性）
        var totalDamage = segments.Sum(s => s.TotalDamage);

        // 5. 构造持久化模型（转换逻辑时间 → 墙钟时间；EndedAt=StartedAt 因为一次性完成）
        var record = new BattleRecord
        {
            Id = battleDomain.Id,
            CharacterId = characterId,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            TotalDamage = totalDamage,
            DurationSeconds = simulateSeconds,
            AttackIntervalSeconds = battleDomain.AttackIntervalSeconds,
            SpecialIntervalSeconds = battleDomain.SpecialIntervalSeconds,
            Segments = segments.Select(s => new BattleSegmentRecord
            {
                Id = Guid.NewGuid(),
                BattleId = battleDomain.Id,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                EventCount = s.EventCount,
                TotalDamage = s.TotalDamage,
                DamageBySourceJson = JsonSerializer.Serialize(s.DamageBySource),
                TagCountersJson = JsonSerializer.Serialize(s.TagCounters),
                ResourceFlowJson = JsonSerializer.Serialize(s.ResourceFlow)
            }).ToList()
        };

        // 6. 持久化（仓储内部：添加 Battle + Segments）
        await _battles.AddAsync(record, ct);

        // 7. 返回战斗 Id 供前端查询 summary / segments
        return record.Id;
    }
}