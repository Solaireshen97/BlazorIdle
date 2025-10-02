using BlazorIdle.Shared.Models;
using System.Net.Http.Json;

namespace BlazorIdle.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    public async Task<CharacterCreated> CreateCharacterAsync(string name, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/characters", new CreateCharacterRequest(name), ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CharacterCreated>(cancellationToken: ct))!;
    }

    public async Task<BattleStartResponse> StartBattleAsync(Guid characterId, double seconds = 10, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/battles/start?characterId={characterId}&seconds={seconds}", null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<BattleStartResponse>(cancellationToken: ct))!;
    }

    public async Task<BattleSummaryDto?> GetBattleSummaryAsync(Guid battleId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<BattleSummaryDto>($"/api/battles/{battleId}/summary", ct);

    public async Task<List<BattleSegmentDto>> GetBattleSegmentsAsync(Guid battleId, CancellationToken ct = default)
        => (await _http.GetFromJsonAsync<List<BattleSegmentDto>>($"/api/battles/{battleId}/segments", ct)) ?? new();
}