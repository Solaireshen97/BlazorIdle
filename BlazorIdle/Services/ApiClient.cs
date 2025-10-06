using BlazorIdle.Shared.Models;
using System.Net.Http.Json;
using System.Text;

namespace BlazorIdle.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    // ===== 角色与同步战斗 =====
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

    // 新：支持 dropMode
    public Task<BattleSummaryResponse?> GetBattleSummaryAsync(Guid battleId, string dropMode = "expected", CancellationToken ct = default)
        => _http.GetFromJsonAsync<BattleSummaryResponse>($"/api/battles/{battleId}/summary?dropMode={Uri.EscapeDataString(dropMode)}", ct);

    public async Task<List<BattleSegmentDto>> GetBattleSegmentsAsync(Guid battleId, CancellationToken ct = default)
        => (await _http.GetFromJsonAsync<List<BattleSegmentDto>>($"/api/battles/{battleId}/segments", ct)) ?? new();

    // ===== 敌人（动态） =====
    public Task<List<EnemyDto>?> GetEnemiesAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<List<EnemyDto>>("/api/enemies", ct);

    public sealed class EnemyDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Level { get; set; }
        public int MaxHp { get; set; }
        public double Armor { get; set; }
        public double MagicResist { get; set; }
    }

    // ===== Step 战斗 =====
    // 兼容旧签名（不带 mode/dungeonId）
    public async Task<StartStepBattleResponse> StartStepBattleAsync(Guid characterId, double seconds = 30, string? enemyId = null, int enemyCount = 1, ulong? seed = null, CancellationToken ct = default)
        => await StartStepBattleAsync(characterId, seconds, enemyId, enemyCount, seed, mode: null, dungeonId: null, ct: ct);

    // mode: "duration" | "continuous" | "dungeonsingle" | "dungeonloop"
    public async Task<StartStepBattleResponse> StartStepBattleAsync(Guid characterId, double seconds, string? enemyId, int enemyCount, ulong? seed, string? mode, string? dungeonId, CancellationToken ct = default)
    {
        var url = $"/api/battles/step/start?characterId={characterId}&seconds={seconds}&enemyCount={enemyCount}";
        if (!string.IsNullOrWhiteSpace(enemyId)) url += $"&enemyId={Uri.EscapeDataString(enemyId)}";
        if (seed.HasValue) url += $"&seed={seed.Value}";
        if (!string.IsNullOrWhiteSpace(mode)) url += $"&mode={Uri.EscapeDataString(mode)}";
        if (!string.IsNullOrWhiteSpace(dungeonId)) url += $"&dungeonId={Uri.EscapeDataString(dungeonId)}";

        // 某些环境下 null content 会失败，这里发一个空 JSON
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(url, content, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StartStepBattleResponse>(cancellationToken: ct))!;
    }

    // 新：支持 dropMode
    public Task<StepStatusResponse?> GetStepBattleStatusAsync(Guid battleId, string dropMode = "expected", CancellationToken ct = default)
        => _http.GetFromJsonAsync<StepStatusResponse>($"/api/battles/step/{battleId}/status?dropMode={Uri.EscapeDataString(dropMode)}", ct);

    public async Task<List<StepBattleSegmentDto>> GetStepBattleSegmentsAsync(Guid battleId, int since = 0, CancellationToken ct = default)
        => (await _http.GetFromJsonAsync<List<StepBattleSegmentDto>>($"/api/battles/step/{battleId}/segments?since={since}", ct)) ?? new();

    public async Task<StopStepBattleResponse> StopStepBattleAsync(Guid battleId, CancellationToken ct = default)
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"/api/battles/step/{battleId}/stop", content, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StopStepBattleResponse>(cancellationToken: ct))!;
    }

    public Task<StepBattleDebugDto?> GetStepBattleDebugAsync(Guid battleId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<StepBattleDebugDto>($"/api/battles/step/{battleId}/debug", ct);

    // ===== 回放（基于历史记录） =====
    public async Task<StartStepBattleResponse> StartReplayStepBattleAsync(Guid sourceBattleRecordId, double? seconds = null, int enemyCount = 1, CancellationToken ct = default)
    {
        var url = $"/api/battles/replay/{sourceBattleRecordId}/start?enemyCount={enemyCount}";
        if (seconds.HasValue) url += $"&seconds={seconds.Value}";
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(url, content, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StartStepBattleResponse>(cancellationToken: ct))!;
    }

    // ===== 批量模拟 =====
    public Task<SimulateResponse?> SimulateAsync(SimulateRequest req, CancellationToken ct = default)
        => _http.PostAsJsonAsync("/api/simulation", req, ct)
                .ContinueWith(async t =>
                {
                    var resp = await t;
                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadFromJsonAsync<SimulateResponse>(cancellationToken: ct);
                }).Unwrap();
}

// ====== Step DTOs（保留运行中需要的） ======
public sealed class StartStepBattleResponse { public Guid BattleId { get; set; } public ulong Seed { get; set; } public string? EnemyId { get; set; } public int EnemyCount { get; set; } }
public sealed class StopStepBattleResponse { public Guid PersistedBattleId { get; set; } }
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

public enum SimulateMode { Kills, Hours }

public sealed class SimulateRequest
{
    public Guid CharacterId { get; set; }
    public string EnemyId { get; set; } = "dummy";
    public int EnemyCount { get; set; } = 1;
    public SimulateMode Mode { get; set; } = SimulateMode.Kills;
    public double Value { get; set; } = 10;
    public double SampleSeconds { get; set; } = 20;
    public ulong? Seed { get; set; }
}

public sealed class SimulateResponse
{
    public Guid CharacterId { get; set; }
    public Profession Profession { get; set; }
    public string EnemyId { get; set; } = "dummy";
    public int EnemyCount { get; set; }
    public SimulateMode Mode { get; set; }
    public double Value { get; set; }
    public double SampleSeconds { get; set; }
    public int Runs { get; set; }
    public double TotalSimulatedSeconds { get; set; }
    public long TotalDamage { get; set; }
    public int TotalKills { get; set; }
    public double AvgDps { get; set; }
    public double KillsPerHour { get; set; }
    public double? AvgTtk { get; set; }
    public double? TtkP50 { get; set; }
    public double? TtkP90 { get; set; }
    public double? TtkP95 { get; set; }
    public double? TtkP99 { get; set; }
}