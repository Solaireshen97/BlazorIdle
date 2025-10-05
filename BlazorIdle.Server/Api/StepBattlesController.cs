using System.Text.Json;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Skills;
using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/battles/step")]
public class StepBattlesController : ControllerBase
{
    private readonly StepBattleCoordinator _coord;
    private readonly ICharacterRepository _characters;

    public StepBattlesController(StepBattleCoordinator coord, ICharacterRepository characters)
    {
        _coord = coord;
        _characters = characters;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromQuery] Guid characterId, [FromQuery] double seconds = 30, [FromQuery] ulong? seed = null, [FromQuery] string? enemyId = null, [FromQuery] int enemyCount = 1)
    {
        var c = await _characters.GetAsync(characterId);
        if (c is null) return NotFound("Character not found.");
        var profession = c.Profession;

        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        var attrs = new PrimaryAttributes(c.Strength, c.Agility, c.Intellect, c.Stamina);
        var derived = StatsBuilder.BuildDerived(profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        ulong finalSeed = seed ?? DeriveSeed(characterId);

        var id = _coord.Start(characterId, profession, stats, seconds, finalSeed, enemyId, enemyCount);
        return Ok(new { battleId = id, seed = finalSeed, enemyId = enemyId ?? "dummy", enemyCount });
    }

    [HttpGet("{id:guid}/status")]
    public ActionResult<object> Status(Guid id)
    {
        var (found, s) = _coord.GetStatus(id);
        if (!found) return NotFound();
        return Ok(s);
    }

    [HttpGet("{id:guid}/segments")]
    public ActionResult<IEnumerable<object>> Segments(Guid id, [FromQuery] int since = 0)
    {
        var (found, segments) = _coord.GetSegments(id, since);
        if (!found) return NotFound();
        return Ok(segments);
    }

    [HttpPost("{id:guid}/stop")]
    public async Task<IActionResult> Stop(Guid id, CancellationToken ct)
    {
        var (ok, persistedId) = await _coord.StopAndFinalizeAsync(id, ct);
        if (!ok) return NotFound();
        return Ok(new { persistedBattleId = persistedId });
    }

    // 新增：运行态调试
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

        // Tracks
        foreach (var t in ctx.Tracks)
        {
            dto.Tracks.Add(new StepBattleDebugDto.TrackDebugDto
            {
                Type = t.TrackType.ToString().ToLowerInvariant(),
                BaseInterval = t.CurrentInterval * t.HasteFactor, // 反推 baseInterval
                HasteFactor = t.HasteFactor,
                CurrentInterval = t.CurrentInterval,
                NextTriggerAt = t.NextTriggerAt
            });
        }

        // Resources（最小：尝试 rage/focus，存在则回传）
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

        // Buffs
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

        // AutoCast + Skills
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

            var ext = def as SkillDefinitionExt;
            var damageType = ext?.DamageType.ToString().ToLowerInvariant() ?? "physical";

            acDto.Skills.Add(new StepBattleDebugDto.SkillDebugDto
            {
                Id = def.Id,
                Name = def.Name,
                Priority = def.Priority,
                MaxCharges = def.MaxCharges,
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
                DamageType = damageType,
                BaseDamage = def.BaseDamage
            });
        }

        dto.AutoCast = acDto;

        // Encounter（主目标为组内 PrimaryAlive）
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