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

    // 修改：增加 enemyId 参数并拼到查询串
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
}