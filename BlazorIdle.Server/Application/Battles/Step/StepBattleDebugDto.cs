using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Application.Battles.Step;

public sealed class StepBattleDebugDto
{
    public Guid StepBattleId { get; set; }
    public double Now { get; set; }
    public long RngIndex { get; set; }
    public int SchedulerCount { get; set; }

    public List<TrackDebugDto> Tracks { get; set; } = new();
    public Dictionary<string, ResourceDebugDto> Resources { get; set; } = new();
    public List<BuffDebugDto> Buffs { get; set; } = new();
    public AutoCastDebugDto AutoCast { get; set; } = new();
    public EncounterDebugDto Encounter { get; set; } = new();

    public sealed class TrackDebugDto
    {
        public string Type { get; set; } = "";
        public double BaseInterval { get; set; }
        public double HasteFactor { get; set; }
        public double CurrentInterval { get; set; }
        public double NextTriggerAt { get; set; }
    }

    public sealed class ResourceDebugDto
    {
        public int Current { get; set; }
        public int Max { get; set; }
    }

    public sealed class BuffDebugDto
    {
        public string Id { get; set; } = "";
        public int Stacks { get; set; }
        public double ExpiresAt { get; set; }
        public double? NextTickAt { get; set; }
        public double TickIntervalSeconds { get; set; }
        public double HasteSnapshot { get; set; }
        public double TickBasePerStack { get; set; }
        public string PeriodicType { get; set; } = "";
        public string PeriodicDamageType { get; set; } = "";
    }

    public sealed class AutoCastDebugDto
    {
        public bool IsCasting { get; set; }
        public double CastingUntil { get; set; }
        public bool CastingSkillLocksAttack { get; set; }
        public long? CurrentCastId { get; set; }
        public double GlobalCooldownUntil { get; set; }
        public List<SkillDebugDto> Skills { get; set; } = new();
    }

    public sealed class SkillDebugDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Priority { get; set; }
        public int MaxCharges { get; set; }
        public int Charges { get; set; }
        public double? NextChargeReadyAt { get; set; }
        public double NextAvailableTime { get; set; }
        public double CooldownSeconds { get; set; }
        public double CastTimeSeconds { get; set; }
        public double GcdSeconds { get; set; }
        public bool OffGcd { get; set; }
        public int CostAmount { get; set; }
        public string? CostResourceId { get; set; }
        public double AttackPowerCoef { get; set; }
        public double SpellPowerCoef { get; set; }
        public string DamageType { get; set; } = "physical";
        public int BaseDamage { get; set; }
    }

    public sealed class EncounterDebugDto
    {
        public string EnemyId { get; set; } = "";
        public int EnemyLevel { get; set; }
        public int EnemyMaxHp { get; set; }
        public int CurrentHp { get; set; }
        public bool IsDead { get; set; }
        public double? KillTime { get; set; }
        public int Overkill { get; set; }
        public int AliveCount { get; set; }
        public int TotalCount { get; set; }
    }
}