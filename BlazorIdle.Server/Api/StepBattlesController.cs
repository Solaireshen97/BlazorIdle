using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Equipment.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/battles/step")]
public class StepBattlesController : ControllerBase
{
    private readonly StepBattleCoordinator _coord;
    private readonly ICharacterRepository _characters;
    private readonly IBattleRepository _battles; // 新增：用于 Stop 的兜底查询
    private readonly EquipmentStatsIntegration _equipmentStats;

    public StepBattlesController(
        StepBattleCoordinator coord, 
        ICharacterRepository characters, 
        IBattleRepository battles,
        EquipmentStatsIntegration equipmentStats)
    {
        _coord = coord;
        _characters = characters;
        _battles = battles;
        _equipmentStats = equipmentStats;
    }

    // 支持刷新等待参数：
    // continuous: respawnDelay
    // dungeon: waveDelay / runDelay
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

        // 构建包含装备加成的完整属性
        var attrs = new PrimaryAttributes(c.Strength, c.Agility, c.Intellect, c.Stamina);
        var stats = await _equipmentStats.BuildStatsWithEquipmentAsync(characterId, profession, attrs);

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

    [HttpGet("{id:guid}/status")]
    public ActionResult<object> Status(Guid id, [FromQuery] string? dropMode = null)
    {
        var (found, s) = _coord.GetStatus(id, dropMode);
        if (!found) return NotFound();
        return Ok(s);
    }

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

    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}