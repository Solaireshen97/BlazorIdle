using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 步进战斗控制器
/// </summary>
/// <remarks>
/// 提供基于步进式战斗引擎的战斗管理功能，支持实时推进和状态查询。
/// 
/// <strong>核心功能</strong>：
/// 1. 启动战斗 - 创建并启动步进式战斗实例
/// 2. 查询状态 - 获取战斗当前状态和统计
/// 3. 获取段数据 - 查询战斗事件段（增量获取）
/// 4. 停止战斗 - 结束战斗并持久化结果
/// 5. 调试信息 - 获取详细的战斗内部状态
/// 
/// <strong>步进式战斗特点</strong>：
/// - 服务端驱动的事件调度
/// - 基于时间的精确推进
/// - 支持暂停和恢复
/// - 实时状态查询
/// - 段数据增量获取
/// 
/// <strong>战斗模式</strong>：
/// - Duration: 时长限制模式
/// - Continuous: 持续战斗（自动重生敌人）
/// - DungeonSingle: 单次地下城
/// - DungeonLoop: 循环地下城
/// 
/// <strong>与普通战斗的区别</strong>：
/// - 普通战斗：一次性完整模拟，适合快速结算
/// - 步进战斗：逐步推进，适合实时监控和调试
/// 
/// <strong>使用场景</strong>：
/// - 开发调试（查看战斗细节）
/// - 性能测试（分析战斗性能）
/// - 实时战斗（需要逐帧更新的战斗）
/// </remarks>
[ApiController]
[Route("api/battles/step")]
public class StepBattlesController : ControllerBase
{
    private readonly StepBattleCoordinator _coord;
    private readonly ICharacterRepository _characters;
    private readonly IBattleRepository _battles; // 用于 Stop 的兜底查询

    public StepBattlesController(StepBattleCoordinator coord, ICharacterRepository characters, IBattleRepository battles)
    {
        _coord = coord;
        _characters = characters;
        _battles = battles;
    }

    /// <summary>
    /// 启动步进式战斗
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="seconds">战斗时长（秒），默认30秒</param>
    /// <param name="seed">随机种子，null则自动生成</param>
    /// <param name="enemyId">敌人ID，null则使用默认敌人</param>
    /// <param name="enemyCount">敌人数量，默认1</param>
    /// <param name="mode">战斗模式：duration/continuous/dungeon/dungeonloop，默认duration</param>
    /// <param name="dungeonId">地下城ID（仅dungeon模式时需要）</param>
    /// <param name="respawnDelay">敌人重生延迟（秒，continuous模式）</param>
    /// <param name="waveDelay">波次间延迟（秒，dungeon模式）</param>
    /// <param name="runDelay">副本间延迟（秒，dungeonloop模式）</param>
    /// <returns>战斗ID和配置信息</returns>
    /// <response code="200">成功启动步进战斗</response>
    /// <response code="400">参数错误</response>
    /// <response code="404">角色不存在</response>
    /// <remarks>
    /// POST /api/battles/step/start?characterId={id}&amp;seconds=60&amp;mode=continuous
    /// 
    /// 创建并启动一个步进式战斗实例。
    /// 
    /// <strong>战斗模式详解</strong>：
    /// 
    /// 1. Duration（时长限制）
    /// - 战斗持续固定时长后结束
    /// - 适用于快速测试
    /// 
    /// 2. Continuous（持续战斗）
    /// - 敌人死亡后自动重生
    /// - 持续战斗直到手动停止或达到时长限制
    /// - respawnDelay: 控制敌人重生延迟
    /// 
    /// 3. DungeonSingle（单次地下城）
    /// - 完成所有波次后结束
    /// - 需要指定 dungeonId
    /// - waveDelay: 控制波次间延迟
    /// 
    /// 4. DungeonLoop（循环地下城）
    /// - 完成后重新开始
    /// - runDelay: 控制副本间延迟
    /// 
    /// <strong>延迟参数</strong>：
    /// - respawnDelay: 敌人重生延迟（continuous模式）
    /// - waveDelay: 波次切换延迟（dungeon模式）
    /// - runDelay: 副本重置延迟（dungeonloop模式）
    /// 
    /// 示例请求：
    /// ```
    /// POST /api/battles/step/start?characterId=550e8400-e29b-41d4-a716-446655440000&amp;seconds=120&amp;mode=continuous&amp;enemyId=goblin&amp;enemyCount=3&amp;respawnDelay=5
    /// ```
    /// 
    /// 响应示例：
    /// ```json
    /// {
    ///   "battleId": "750e8400-e29b-41d4-a716-446655440002",
    ///   "seed": 9876543210,
    ///   "enemyId": "goblin",
    ///   "enemyCount": 3,
    ///   "mode": "continuous",
    ///   "respawnDelay": 5
    /// }
    /// ```
    /// </remarks>
    [HttpPost("start")]
    public async Task<IActionResult> Start(
        [FromQuery] Guid characterId,
        [FromQuery] double seconds = 30,
        [FromQuery] ulong? seed = null,
        [FromQuery] string? enemyId = null,
        [FromQuery] int enemyCount = 1,
        [FromQuery] string? mode = null,
        [FromQuery] string? dungeonId = null,
        [FromQuery] double? respawnDelay = null,
        [FromQuery] double? waveDelay = null,
        [FromQuery] double? runDelay = null)
    {
        var c = await _characters.GetAsync(characterId);
        if (c is null) return NotFound("Character not found.");
        var profession = c.Profession;

        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        var attrs = new PrimaryAttributes(c.Strength, c.Agility, c.Intellect, c.Stamina);
        var derived = StatsBuilder.BuildDerived(profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        ulong finalSeed = seed ?? DeriveSeed(characterId);

        StepBattleMode parsedMode = StepBattleMode.Duration;
        if (!string.IsNullOrWhiteSpace(mode))
        {
            var m = mode.Trim().ToLowerInvariant();
            parsedMode = m switch
            {
                "continuous" => StepBattleMode.Continuous,
                "dungeon" => StepBattleMode.DungeonSingle,
                "dungeonloop" => StepBattleMode.DungeonLoop,
                _ => StepBattleMode.Duration
            };
        }

        if ((parsedMode == StepBattleMode.DungeonSingle || parsedMode == StepBattleMode.DungeonLoop) && string.IsNullOrWhiteSpace(dungeonId))
            return BadRequest("dungeonId is required for dungeon modes.");

        var id = _coord.Start(
            characterId, profession, stats, seconds, finalSeed, enemyId, enemyCount,
            parsedMode, dungeonId,
            continuousRespawnDelaySeconds: respawnDelay,
            dungeonWaveDelaySeconds: waveDelay,
            dungeonRunDelaySeconds: runDelay,
            stamina: c.Stamina
        );

        return Ok(new
        {
            battleId = id,
            seed = finalSeed,
            enemyId = enemyId ?? "dummy",
            enemyCount,
            mode = parsedMode.ToString().ToLowerInvariant(),
            dungeonId,
            respawnDelay,
            waveDelay,
            runDelay
        });
    }

    /// <summary>
    /// 获取步进战斗状态
    /// </summary>
    /// <param name="id">战斗ID</param>
    /// <param name="dropMode">掉落模式：expected/sampled，null使用默认</param>
    /// <returns>战斗当前状态和统计信息</returns>
    /// <response code="200">成功返回战斗状态</response>
    /// <response code="404">战斗不存在或已结束</response>
    /// <remarks>
    /// GET /api/battles/step/{id}/status?dropMode=expected
    /// 
    /// 查询步进战斗的实时状态。
    /// 
    /// <strong>返回内容</strong>：
    /// - 当前游戏时间
    /// - 角色状态（生命值、资源等）
    /// - 敌人状态
    /// - 战斗统计（总伤害、击杀数）
    /// - 掉落预览（基于dropMode）
    /// 
    /// <strong>dropMode参数</strong>：
    /// - expected: 基于概率的期望掉落（稳定预测）
    /// - sampled: 实际随机掉落（真实结果）
    /// 
    /// 示例请求：
    /// ```
    /// GET /api/battles/step/750e8400-e29b-41d4-a716-446655440002/status
    /// ```
    /// </remarks>
    [HttpGet("{id:guid}/status")]
    public ActionResult<object> Status(Guid id, [FromQuery] string? dropMode = null)
    {
        var (found, s) = _coord.GetStatus(id, dropMode);
        if (!found) return NotFound();
        return Ok(s);
    }

    /// <summary>
    /// 获取战斗事件段（增量获取）
    /// </summary>
    /// <param name="id">战斗ID</param>
    /// <param name="since">起始段索引，默认0（从头开始）</param>
    /// <returns>战斗事件段列表</returns>
    /// <response code="200">成功返回事件段列表（可能为空数组）</response>
    /// <response code="404">战斗不存在</response>
    /// <remarks>
    /// GET /api/battles/step/{id}/segments?since=0
    /// 
    /// 增量获取战斗事件段，用于客户端实时更新。
    /// 
    /// <strong>段（Segment）概念</strong>：
    /// - 战斗事件按时间或数量聚合成段
    /// - 每段包含一批相关事件
    /// - 客户端通过段索引增量拉取
    /// 
    /// <strong>增量拉取流程</strong>：
    /// 1. 首次请求：since=0，获取所有段
    /// 2. 后续请求：since=lastIndex+1，只获取新段
    /// 3. 重复步骤2直到战斗结束
    /// 
    /// <strong>段内容</strong>：
    /// - 段索引
    /// - 时间范围（开始时间、结束时间）
    /// - 事件列表（攻击、技能、伤害等）
    /// - 段内统计（总伤害、击杀数）
    /// 
    /// <strong>性能优化</strong>：
    /// - 返回空数组（[]）而非null，避免客户端null检查
    /// - 只返回since之后的新段，减少传输量
    /// 
    /// 示例请求：
    /// ```
    /// # 首次获取
    /// GET /api/battles/step/750e8400-e29b-41d4-a716-446655440002/segments?since=0
    /// 
    /// # 增量获取（已有10个段）
    /// GET /api/battles/step/750e8400-e29b-41d4-a716-446655440002/segments?since=10
    /// ```
    /// 
    /// 响应示例：
    /// ```json
    /// [
    ///   {
    ///     "index": 10,
    ///     "startTime": 50.0,
    ///     "endTime": 55.0,
    ///     "events": [...],
    ///     "totalDamage": 1200
    ///   }
    /// ]
    /// ```
    /// </remarks>
    [HttpGet("{id:guid}/segments")]
    [ProducesResponseType(typeof(List<StepBattleSegmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<List<StepBattleSegmentDto>> Segments(Guid id, [FromQuery] int since = 0)
    {
        var (found, segments) = _coord.GetSegments(id, since);
        if (!found) return NotFound();

        // 明确保证返回 [] 而不是 null，避免客户端拿到 null
        return Ok(segments ?? new List<StepBattleSegmentDto>());
    }

    /// <summary>
    /// 停止步进战斗并持久化结果
    /// </summary>
    /// <param name="id">战斗ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>持久化的战斗记录ID</returns>
    /// <response code="200">成功停止战斗</response>
    /// <response code="404">战斗不存在</response>
    /// <remarks>
    /// POST /api/battles/step/{id}/stop
    /// 
    /// 停止一个正在运行的步进战斗，并将结果持久化到数据库。
    /// 
    /// <strong>停止流程</strong>：
    /// 1. 检查战斗是否在内存中（正在运行）
    /// 2. 如果在内存：
    ///    - 停止战斗推进
    ///    - 计算最终统计
    ///    - 发放奖励
    ///    - 持久化到数据库
    /// 3. 如果不在内存（已自动结束）：
    ///    - 查询数据库中的战斗记录（幂等兜底）
    ///    - 返回已持久化的记录ID
    /// 
    /// <strong>幂等性</strong>：
    /// - 重复调用不会重复发放奖励
    /// - 如果战斗已结束，直接返回记录ID
    /// - 支持后台自动finalize后的查询
    /// 
    /// <strong>自动停止</strong>：
    /// 以下情况会自动停止战斗：
    /// - 达到时长限制
    /// - 完成所有副本波次（非循环模式）
    /// - 角色死亡
    /// 
    /// 示例请求：
    /// ```
    /// POST /api/battles/step/750e8400-e29b-41d4-a716-446655440002/stop
    /// ```
    /// 
    /// 响应示例：
    /// ```json
    /// {
    ///   "persistedBattleId": "750e8400-e29b-41d4-a716-446655440002"
    /// }
    /// ```
    /// </remarks>
    [HttpPost("{id:guid}/stop")]
    public async Task<IActionResult> Stop(Guid id, CancellationToken ct)
    {
        // 第一种：仍在内存中，正常走协调器的 Stop & Finalize
        if (_coord.TryGet(id, out var rb) && rb is not null)
        {
            var (ok, persistedId) = await _coord.StopAndFinalizeAsync(id, ct);
            if (!ok) return NotFound();
            return Ok(new { persistedBattleId = persistedId });
        }

        // 第二种：已经被后台自动 Finalize，或重启后不在内存，尝试直接查库（幂等兜底）
        var rec = await _battles.GetWithSegmentsAsync(id, ct);
        if (rec is not null)
        {
            return Ok(new { persistedBattleId = rec.Id });
        }

        return NotFound();
    }

    /// <summary>
    /// 获取步进战斗调试信息（详细内部状态）
    /// </summary>
    /// <param name="id">战斗ID</param>
    /// <returns>详细的战斗内部状态信息</returns>
    /// <response code="200">成功返回调试信息</response>
    /// <response code="404">战斗不存在或已结束</response>
    /// <remarks>
    /// GET /api/battles/step/{id}/debug
    /// 
    /// 获取步进战斗的详细内部状态，用于开发调试和问题排查。
    /// 
    /// <strong>返回的调试信息</strong>：
    /// 
    /// 1. **基础信息**
    ///    - 当前游戏时间
    ///    - RNG索引
    ///    - 事件调度器队列大小
    /// 
    /// 2. **轨道（Tracks）状态**
    ///    - 攻击轨道和特殊轨道
    ///    - 基础间隔、急速系数
    ///    - 当前间隔、下次触发时间
    /// 
    /// 3. **资源状态**
    ///    - 怒气、集中等资源当前值/最大值
    ///    - 资源溢出情况
    /// 
    /// 4. **Buff状态**
    ///    - 所有激活的Buff
    ///    - 层数、到期时间
    ///    - 周期伤害信息
    /// 
    /// 5. **技能自动施放状态**
    ///    - 是否正在施法
    ///    - 施法锁定时间
    ///    - 全局冷却时间
    ///    - 每个技能的详细状态
    ///      - 冷却时间、充能数
    ///      - 资源消耗、伤害系数
    ///      - 优先级、施法时间
    /// 
    /// 6. **遭遇战状态**
    ///    - 当前敌人信息
    ///    - 敌人生命值
    ///    - 击杀时间、过量伤害
    ///    - 存活敌人数/总数
    /// 
    /// 7. **事件收集器状态**
    ///    - 当前段开始时间
    ///    - 最后事件时间
    ///    - 段内事件数量
    /// 
    /// <strong>使用场景</strong>：
    /// - 调试战斗逻辑问题
    /// - 分析性能瓶颈
    /// - 验证技能触发时机
    /// - 追踪资源生成和消耗
    /// - 检查Buff状态和层数
    /// 
    /// <strong>注意</strong>：
    /// - 仅用于开发和调试
    /// - 返回的数据量较大
    /// - 生产环境建议关闭或限流
    /// 
    /// 示例请求：
    /// ```
    /// GET /api/battles/step/750e8400-e29b-41d4-a716-446655440002/debug
    /// ```
    /// </remarks>
    [HttpGet("{id:guid}/debug")]
    public ActionResult<StepBattleDebugDto> Debug(Guid id)
    {
        if (!_coord.TryGet(id, out var rb) || rb is null)
            return NotFound();

        var ctx = rb.Context;
        var dto = new StepBattleDebugDto
        {
            StepBattleId = rb.Id,
            Now = ctx.Clock.CurrentTime,
            RngIndex = ctx.Rng.Index,
            SchedulerCount = ctx.Scheduler.Count
        };

        foreach (var t in ctx.Tracks)
        {
            dto.Tracks.Add(new StepBattleDebugDto.TrackDebugDto
            {
                Type = t.TrackType.ToString().ToLowerInvariant(),
                BaseInterval = t.BaseInterval,
                HasteFactor = t.HasteFactor,
                CurrentInterval = t.CurrentInterval,
                NextTriggerAt = t.NextTriggerAt
            });
        }

        var resourceIds = new[] { "rage", "focus" };
        foreach (var rid in resourceIds)
        {
            if (ctx.Resources.TryGet(rid, out var bucket))
            {
                dto.Resources[rid] = new StepBattleDebugDto.ResourceDebugDto
                {
                    Current = bucket.Current,
                    Max = bucket.Max
                };
            }
        }

        foreach (var b in ctx.Buffs.Active)
        {
            dto.Buffs.Add(new StepBattleDebugDto.BuffDebugDto
            {
                Id = b.Definition.Id,
                Stacks = b.Stacks,
                ExpiresAt = b.ExpiresAt,
                NextTickAt = b.NextTickAt,
                TickIntervalSeconds = b.TickIntervalSeconds,
                HasteSnapshot = b.HasteSnapshot,
                TickBasePerStack = b.TickBasePerStack,
                PeriodicType = b.Definition.PeriodicType.ToString().ToLowerInvariant(),
                PeriodicDamageType = b.Definition.PeriodicDamageType.ToString().ToLowerInvariant()
            });
        }

        var ac = ctx.AutoCaster;
        var acDto = new StepBattleDebugDto.AutoCastDebugDto
        {
            IsCasting = ac.IsCasting,
            CastingUntil = ac.CastingUntil,
            CastingSkillLocksAttack = ac.CastingSkillLocksAttack,
            CurrentCastId = ac.CurrentCastId,
            GlobalCooldownUntil = ac.GlobalCooldownUntil
        };

        foreach (var slot in ac.Slots)
        {
            var def = slot.Runtime.Definition;
            var rt = slot.Runtime;
            var ext = def as Domain.Combat.Skills.SkillDefinitionExt;
            acDto.Skills.Add(new StepBattleDebugDto.SkillDebugDto
            {
                Id = def.Id,
                Name = def.Name,
                Priority = def.Priority,
                MaxCharges = rt.Charges, // note: this is displaying current charges/max below
                Charges = rt.Charges,
                NextChargeReadyAt = rt.NextChargeReadyAt,
                NextAvailableTime = rt.NextAvailableTime,
                CooldownSeconds = def.CooldownSeconds,
                CastTimeSeconds = def.CastTimeSeconds,
                GcdSeconds = def.GcdSeconds,
                OffGcd = def.OffGcd,
                CostAmount = def.CostAmount,
                CostResourceId = def.CostResourceId,
                AttackPowerCoef = def.AttackPowerCoef,
                SpellPowerCoef = def.SpellPowerCoef,
                DamageType = (ext?.DamageType.ToString().ToLowerInvariant() ?? "physical"),
                BaseDamage = def.BaseDamage
            });
        }
        dto.AutoCast = acDto;

        var enc = ctx.Encounter;
        var grp = ctx.EncounterGroup;
        dto.Encounter = new StepBattleDebugDto.EncounterDebugDto
        {
            EnemyId = enc?.Enemy.Id ?? (grp?.PrimaryAlive()?.Enemy.Id ?? "unknown"),
            EnemyLevel = enc?.Enemy.Level ?? (grp?.PrimaryAlive()?.Enemy.Level ?? 0),
            EnemyMaxHp = enc?.Enemy.MaxHp ?? (grp?.PrimaryAlive()?.Enemy.MaxHp ?? 0),
            CurrentHp = enc?.CurrentHp ?? (grp?.PrimaryAlive()?.CurrentHp ?? 0),
            IsDead = enc?.IsDead ?? (grp?.PrimaryAlive()?.IsDead ?? false),
            KillTime = enc?.KillTime,
            Overkill = enc?.Overkill ?? 0,
            AliveCount = grp?.All.Count(e => !e.IsDead) ?? (enc is not null ? (enc.IsDead ? 0 : 1) : 0),
            TotalCount = grp?.All.Count ?? (enc is not null ? 1 : 0)
        };

        dto.Collector = new StepBattleDebugDto.CollectorDebugDto
        {
            SegmentStart = ctx.SegmentCollector.SegmentStart,
            LastEventTime = ctx.SegmentCollector.LastEventTime,
            EventCount = ctx.SegmentCollector.EventCount
        };

        return Ok(dto);
    }

    /// <summary>
    /// 根据角色ID生成随机种子
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>生成的随机种子</returns>
    /// <remarks>
    /// 基于角色ID和当前时间生成唯一的随机种子，确保：
    /// 1. 不同角色生成不同种子
    /// 2. 同一角色不同时间生成不同种子
    /// 3. 种子具有足够的随机性
    /// </remarks>
    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}