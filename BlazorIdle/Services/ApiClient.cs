using System.Net.Http.Json;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    // ========== 同步战斗相关 ==========
    public async Task<CharacterCreated> CreateCharacterAsync(string name, Profession profession, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/characters", new CreateCharacterRequest(name, profession), ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CharacterCreated>(cancellationToken: ct))!;
    }

    public async Task<BattleStartResponse> StartBattleAsync(Guid characterId, double seconds = 10, string? enemyId = null, CancellationToken ct = default)
    {
        var url = $"/api/battles/start?characterId={characterId}&seconds={seconds}";
        if (!string.IsNullOrWhiteSpace(enemyId)) url += $"&enemyId={Uri.EscapeDataString(enemyId)}";
        var resp = await _http.PostAsync(url, null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<BattleStartResponse>(cancellationToken: ct))!;
    }

    public Task<BattleSummaryDto?> GetBattleSummaryAsync(Guid battleId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<BattleSummaryDto>($"/api/battles/{battleId}/summary", ct);

    public async Task<List<BattleSegmentDto>> GetBattleSegmentsAsync(Guid battleId, CancellationToken ct = default)
        => (await _http.GetFromJsonAsync<List<BattleSegmentDto>>($"/api/battles/{battleId}/segments", ct)) ?? new();

    // ========== Step 战斗相关 ==========
    public async Task<StartStepBattleResponse> StartStepBattleAsync(
        Guid characterId,
        double seconds = 30,
        string? enemyId = null,
        int enemyCount = 1,
        ulong? seed = null,
        CancellationToken ct = default)
    {
        var url = $"/api/battles/step/start?characterId={characterId}&seconds={seconds}&enemyCount={enemyCount}";
        if (!string.IsNullOrWhiteSpace(enemyId)) url += $"&enemyId={Uri.EscapeDataString(enemyId)}";
        if (seed.HasValue) url += $"&seed={seed.Value}";
        var resp = await _http.PostAsync(url, null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StartStepBattleResponse>(cancellationToken: ct))!;
    }

    public Task<StepBattleStatusDto?> GetStepBattleStatusAsync(Guid battleId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<StepBattleStatusDto>($"/api/battles/step/{battleId}/status", ct);

    public async Task<List<StepBattleSegmentDto>> GetStepBattleSegmentsAsync(Guid battleId, int since = 0, CancellationToken ct = default)
        => (await _http.GetFromJsonAsync<List<StepBattleSegmentDto>>($"/api/battles/step/{battleId}/segments?since={since}", ct)) ?? new();

    public async Task<StopStepBattleResponse> StopStepBattleAsync(Guid battleId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/battles/step/{battleId}/stop", null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StopStepBattleResponse>(cancellationToken: ct))!;
    }

    public Task<StepBattleDebugDto?> GetStepBattleDebugAsync(Guid battleId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<StepBattleDebugDto>($"/api/battles/step/{battleId}/debug", ct);
}

// ========== Step DTOs（前端自带）==========
public sealed class StartStepBattleResponse
{
    public Guid BattleId { get; set; }
    public ulong Seed { get; set; }
    public string? EnemyId { get; set; }
    public int EnemyCount { get; set; }
}

public sealed class StopStepBattleResponse
{
    public Guid PersistedBattleId { get; set; }
}

public sealed class StepBattleStatusDto
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public Profession Profession { get; set; }
    public string EnemyId { get; set; } = "dummy";
    public int EnemyCount { get; set; }
    public double SimulatedSeconds { get; set; }
    public double TargetSeconds { get; set; }
    public bool Completed { get; set; }
    public int TotalDamage { get; set; }
    public double Dps { get; set; }
    public int SegmentCount { get; set; }
    public string Seed { get; set; } = "0";
    public long SeedIndexStart { get; set; }
    public long SeedIndexEnd { get; set; }
    public bool Killed { get; set; }
    public double? KillTimeSeconds { get; set; }
    public int OverkillDamage { get; set; }
    public Guid? PersistedBattleId { get; set; }
}

public sealed class StepBattleSegmentDto
{
    public int Index { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public int EventCount { get; set; }
    public int TotalDamage { get; set; }
    public Dictionary<string, int> DamageBySource { get; set; } = new();
    public Dictionary<string, int> DamageByType { get; set; } = new();
    public Dictionary<string, int> ResourceFlow { get; set; } = new();
}

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
    public CollectorDebugDto Collector { get; set; } = new();

    public sealed class TrackDebugDto { public string Type { get; set; } = ""; public double BaseInterval { get; set; } public double HasteFactor { get; set; } public double CurrentInterval { get; set; } public double NextTriggerAt { get; set; } }
    public sealed class ResourceDebugDto { public int Current { get; set; } public int Max { get; set; } }
    public sealed class BuffDebugDto { public string Id { get; set; } = ""; public int Stacks { get; set; } public double ExpiresAt { get; set; } public double? NextTickAt { get; set; } public double TickIntervalSeconds { get; set; } public double HasteSnapshot { get; set; } public double TickBasePerStack { get; set; } public string PeriodicType { get; set; } = ""; public string PeriodicDamageType { get; set; } = ""; }
    public sealed class AutoCastDebugDto { public bool IsCasting { get; set; } public double CastingUntil { get; set; } public bool CastingSkillLocksAttack { get; set; } public long? CurrentCastId { get; set; } public double GlobalCooldownUntil { get; set; } public List<SkillDebugDto> Skills { get; set; } = new(); }
    public sealed class SkillDebugDto { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public int Priority { get; set; } public int MaxCharges { get; set; } public int Charges { get; set; } public double? NextChargeReadyAt { get; set; } public double NextAvailableTime { get; set; } public double CooldownSeconds { get; set; } public double CastTimeSeconds { get; set; } public double GcdSeconds { get; set; } public bool OffGcd { get; set; } public int CostAmount { get; set; } public string? CostResourceId { get; set; } public double AttackPowerCoef { get; set; } public double SpellPowerCoef { get; set; } public string DamageType { get; set; } = "physical"; public int BaseDamage { get; set; } }
    public sealed class EncounterDebugDto { public string EnemyId { get; set; } = ""; public int EnemyLevel { get; set; } public int EnemyMaxHp { get; set; } public int CurrentHp { get; set; } public bool IsDead { get; set; } public double? KillTime { get; set; } public int Overkill { get; set; } public int AliveCount { get; set; } public int TotalCount { get; set; } }
    public sealed class CollectorDebugDto { public double SegmentStart { get; set; } public double LastEventTime { get; set; } public int EventCount { get; set; } }
}