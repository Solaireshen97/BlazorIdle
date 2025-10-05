using BlazorIdle.Shared.Models;
using System.Net.Http.Json;

namespace BlazorIdle.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    public async Task<CharacterCreated> CreateCharacterAsync(
        string name,
        Profession profession,
        CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            "/api/characters",
            new CreateCharacterRequest(name, profession),
            ct);

        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CharacterCreated>(cancellationToken: ct))!;
    }

    public async Task<BattleStartResponse> StartBattleAsync(
        Guid characterId,
        double seconds = 10,
        string? enemyId = null,
        CancellationToken ct = default)
    {
        var url = $"/api/battles/start?characterId={characterId}&seconds={seconds}";
        if (!string.IsNullOrWhiteSpace(enemyId))
            url += $"&enemyId={Uri.EscapeDataString(enemyId)}";

        var resp = await _http.PostAsync(url, null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<BattleStartResponse>(cancellationToken: ct))!;
    }

    public async Task<BattleSummaryDto?> GetBattleSummaryAsync(Guid battleId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<BattleSummaryDto>($"/api/battles/{battleId}/summary", ct);

    public async Task<List<BattleSegmentDto>> GetBattleSegmentsAsync(Guid battleId, CancellationToken ct = default)
        => (await _http.GetFromJsonAsync<List<BattleSegmentDto>>($"/api/battles/{battleId}/segments", ct)) ?? new();

    // Step APIs
    public async Task<StartStepBattleResponse> StartStepBattleAsync(
        Guid characterId,
        double seconds = 30,
        string? enemyId = null,
        int enemyCount = 1,
        ulong? seed = null,
        CancellationToken ct = default)
    {
        var url = $"/api/battles/step/start?characterId={characterId}&seconds={seconds}&enemyCount={enemyCount}";
        if (!string.IsNullOrWhiteSpace(enemyId))
            url += $"&enemyId={Uri.EscapeDataString(enemyId)}";
        if (seed.HasValue)
            url += $"&seed={seed.Value}";

        var resp = await _http.PostAsync(url, null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StartStepBattleResponse>(cancellationToken: ct))!;
    }

    public async Task<StepBattleStatusDto?> GetStepBattleStatusAsync(Guid battleId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<StepBattleStatusDto>($"/api/battles/step/{battleId}/status", ct);

    public async Task<List<StepBattleSegmentDto>> GetStepBattleSegmentsAsync(Guid battleId, int since = 0, CancellationToken ct = default)
        => (await _http.GetFromJsonAsync<List<StepBattleSegmentDto>>($"/api/battles/step/{battleId}/segments?since={since}", ct)) ?? new();

    // 新增：Stop & Save
    public async Task<StopStepBattleResponse> StopStepBattleAsync(Guid battleId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/battles/step/{battleId}/stop", null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StopStepBattleResponse>(cancellationToken: ct))!;
    }
}

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