using BlazorIdle.Services;
using BlazorIdle.Shared.Models;
using System.Net.Http.Json;
using System.Text;

namespace BlazorIdle.Client.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthService _authService;
    
    public ApiClient(HttpClient http, AuthService authService)
    {
        _http = http;
        _authService = authService;
    }

    // 在请求前设置 Authorization header
    private void SetAuthHeader()
    {
        var token = _authService.GetToken();
        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    // 添加一个包装方法来处理API请求的错误
    private async Task<T?> HandleApiRequestAsync<T>(Func<Task<T?>> apiCall, CancellationToken ct = default)
    {
        try
        {
            SetAuthHeader();
            return await apiCall();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                                              ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // 401 未授权 或 404 未找到资源，可能是令牌无效或用户不存在
            await _authService.LogoutAsync();
            // 注意：在API客户端中重定向可能不是最佳实践，可以改为抛出特定异常
            throw new ApiAuthException("登录状态已过期，请重新登录");
        }
        catch (Exception)
        {
            // 其他异常重新抛出
            throw;
        }
    }

    // ===== 用户管理 =====
    public Task<UserInfoDto?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        return HandleApiRequestAsync(() =>
            _http.GetFromJsonAsync<UserInfoDto>("/api/users/me", ct));
    }

    public async Task<List<UserCharacterDto>> GetUserCharactersAsync(Guid userId, CancellationToken ct = default)
    {
        SetAuthHeader();
        return (await _http.GetFromJsonAsync<List<UserCharacterDto>>($"/api/users/{userId}/characters", ct)) ?? new();
    }

    // ===== 角色与同步战斗 =====
    public async Task<CharacterCreated> CreateCharacterAsync(string name, Profession profession, CancellationToken ct = default)
    {
        SetAuthHeader();
        var resp = await _http.PostAsJsonAsync("/api/characters", new CreateCharacterRequest(name, profession), ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CharacterCreated>(cancellationToken: ct))!;
    }

    public async Task<BattleStartResponse> StartBattleAsync(Guid characterId, double seconds = 10, string? enemyId = null, CancellationToken ct = default)
    {
        SetAuthHeader();
        var url = $"/api/battles/start?characterId={characterId}&seconds={seconds}";
        if (!string.IsNullOrWhiteSpace(enemyId)) url += $"&enemyId={Uri.EscapeDataString(enemyId)}";
        var resp = await _http.PostAsync(url, null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<BattleStartResponse>(cancellationToken: ct))!;
    }

    // 新：支持 dropMode
    public Task<BattleSummaryResponse?> GetBattleSummaryAsync(Guid battleId, string dropMode = "expected", CancellationToken ct = default)
    {
        SetAuthHeader();
        return _http.GetFromJsonAsync<BattleSummaryResponse>($"/api/battles/{battleId}/summary?dropMode={Uri.EscapeDataString(dropMode)}", ct);
    }

    public async Task<List<BattleSegmentDto>> GetBattleSegmentsAsync(Guid battleId, CancellationToken ct = default)
    {
        SetAuthHeader();
        return (await _http.GetFromJsonAsync<List<BattleSegmentDto>>($"/api/battles/{battleId}/segments", ct)) ?? new();
    }

    // ===== 敌人（动态） =====
    public Task<List<EnemyDto>?> GetEnemiesAsync(CancellationToken ct = default)
    {
        SetAuthHeader();
        return _http.GetFromJsonAsync<List<EnemyDto>>("/api/enemies", ct);
    }

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
        SetAuthHeader();
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
    {
        SetAuthHeader();
        return _http.GetFromJsonAsync<StepStatusResponse>($"/api/battles/step/{battleId}/status?dropMode={Uri.EscapeDataString(dropMode)}", ct);
    }

    public async Task<List<StepBattleSegmentDto>> GetStepBattleSegmentsAsync(Guid battleId, int since = 0, CancellationToken ct = default)
    {
        SetAuthHeader();
        return (await _http.GetFromJsonAsync<List<StepBattleSegmentDto>>($"/api/battles/step/{battleId}/segments?since={since}", ct)) ?? new();
    }

    public async Task<StopStepBattleResponse> StopStepBattleAsync(Guid battleId, CancellationToken ct = default)
    {
        SetAuthHeader();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"/api/battles/step/{battleId}/stop", content, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StopStepBattleResponse>(cancellationToken: ct))!;
    }

    public Task<StepBattleDebugDto?> GetStepBattleDebugAsync(Guid battleId, CancellationToken ct = default)
    {
        SetAuthHeader();
        return _http.GetFromJsonAsync<StepBattleDebugDto>($"/api/battles/step/{battleId}/debug", ct);
    }

    // ===== 回放（基于历史记录） =====
    public async Task<StartStepBattleResponse> StartReplayStepBattleAsync(Guid sourceBattleRecordId, double? seconds = null, int enemyCount = 1, CancellationToken ct = default)
    {
        SetAuthHeader();
        var url = $"/api/battles/replay/{sourceBattleRecordId}/start?enemyCount={enemyCount}";
        if (seconds.HasValue) url += $"&seconds={seconds.Value}";
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(url, content, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StartStepBattleResponse>(cancellationToken: ct))!;
    }

    // ===== 批量模拟 =====
    public async Task<SimulateResponse?> SimulateAsync(SimulateRequest req, CancellationToken ct = default)
    {
        SetAuthHeader();
        var resp = await _http.PostAsJsonAsync("/api/simulation", req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SimulateResponse>(cancellationToken: ct);
    }

    // ===== 背包 =====
    public Task<InventoryResponse?> GetInventoryAsync(Guid characterId, CancellationToken ct = default)
    {
        SetAuthHeader();
        return _http.GetFromJsonAsync<InventoryResponse>($"/api/inventory/{characterId}", ct);
    }

    // ===== 活动计划 =====
    public async Task<ActivityPlanDto> CreateCombatPlanAsync(
        Guid characterId,
        int slotIndex = 0,
        string limitType = "duration",
        double? limitValue = null,
        string? enemyId = null,
        int enemyCount = 1,
        double? respawnDelay = null,
        ulong? seed = null,
        CancellationToken ct = default)
    {
        SetAuthHeader();
        var url = $"/api/activity-plans/combat?characterId={characterId}&slotIndex={slotIndex}&limitType={Uri.EscapeDataString(limitType)}&enemyCount={enemyCount}";
        if (limitValue.HasValue) url += $"&limitValue={limitValue.Value}";
        if (!string.IsNullOrWhiteSpace(enemyId)) url += $"&enemyId={Uri.EscapeDataString(enemyId)}";
        if (respawnDelay.HasValue) url += $"&respawnDelay={respawnDelay.Value}";
        if (seed.HasValue) url += $"&seed={seed.Value}";

        var resp = await _http.PostAsync(url, null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ActivityPlanDto>(cancellationToken: ct))!;
    }

    public async Task<ActivityPlanDto> CreateDungeonPlanAsync(
        Guid characterId,
        int slotIndex = 0,
        string limitType = "duration",
        double? limitValue = null,
        string dungeonId = "intro_cave",
        bool loop = false,
        double? waveDelay = null,
        double? runDelay = null,
        ulong? seed = null,
        CancellationToken ct = default)
    {
        SetAuthHeader();
        var url = $"/api/activity-plans/dungeon?characterId={characterId}&slotIndex={slotIndex}&limitType={Uri.EscapeDataString(limitType)}&dungeonId={Uri.EscapeDataString(dungeonId)}&loop={loop}";
        if (limitValue.HasValue) url += $"&limitValue={limitValue.Value}";
        if (waveDelay.HasValue) url += $"&waveDelay={waveDelay.Value}";
        if (runDelay.HasValue) url += $"&runDelay={runDelay.Value}";
        if (seed.HasValue) url += $"&seed={seed.Value}";

        var resp = await _http.PostAsync(url, null, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ActivityPlanDto>(cancellationToken: ct))!;
    }

    public async Task<List<ActivityPlanDto>> GetCharacterPlansAsync(Guid characterId, CancellationToken ct = default)
    {
        SetAuthHeader();
        return (await _http.GetFromJsonAsync<List<ActivityPlanDto>>($"/api/activity-plans/character/{characterId}", ct)) ?? new();
    }

    public async Task<ActivityPlanDto?> GetPlanAsync(Guid planId, CancellationToken ct = default)
    {
        SetAuthHeader();
        return await _http.GetFromJsonAsync<ActivityPlanDto>($"/api/activity-plans/{planId}", ct);
    }

    public async Task<StartPlanResponse> StartPlanAsync(Guid planId, CancellationToken ct = default)
    {
        SetAuthHeader();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"/api/activity-plans/{planId}/start", content, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<StartPlanResponse>(cancellationToken: ct))!;
    }

    public async Task<bool> StopPlanAsync(Guid planId, CancellationToken ct = default)
    {
        SetAuthHeader();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"/api/activity-plans/{planId}/stop", content, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> CancelPlanAsync(Guid planId, CancellationToken ct = default)
    {
        SetAuthHeader();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"/api/activity-plans/{planId}/cancel", content, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeletePlanAsync(Guid planId, CancellationToken ct = default)
    {
        SetAuthHeader();
        var resp = await _http.DeleteAsync($"/api/activity-plans/{planId}", ct);
        return resp.IsSuccessStatusCode;
    }

    // ===== 离线战斗 =====
    /// <summary>
    /// 检查离线收益（不发放）
    /// </summary>
    public Task<OfflineCheckResult?> CheckOfflineAsync(Guid characterId, CancellationToken ct = default)
    {
        SetAuthHeader();
        return _http.GetFromJsonAsync<OfflineCheckResult>($"/api/offline/check?characterId={characterId}", ct);
    }

    /// <summary>
    /// 应用离线结算，实际发放收益
    /// </summary>
    public async Task ApplyOfflineSettlementAsync(Guid characterId, OfflineFastForwardResult settlement, CancellationToken ct = default)
    {
        SetAuthHeader();
        var request = new ApplySettlementRequest
        {
            CharacterId = characterId,
            Settlement = settlement
        };
        var resp = await _http.PostAsJsonAsync("/api/offline/apply", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// 更新心跳时间（记录玩家在线）
    /// </summary>
    public async Task UpdateHeartbeatAsync(Guid characterId, CancellationToken ct = default)
    {
        SetAuthHeader();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"/api/characters/{characterId}/heartbeat", content, ct);
        resp.EnsureSuccessStatusCode();
    }
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

// ===== 活动计划 DTOs =====
public sealed class ActivityPlanDto
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public int SlotIndex { get; set; }
    public int Type { get; set; }  // 1=Combat, 2=Dungeon
    public int LimitType { get; set; }  // 1=Duration, 2=Infinite
    public double? LimitValue { get; set; }
    public int State { get; set; }  // 0=Pending, 1=Running, 2=Completed, 3=Cancelled
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string PayloadJson { get; set; } = "";
    public Guid? BattleId { get; set; }
    public double ExecutedSeconds { get; set; }
}

public sealed class StartPlanResponse
{
    public Guid PlanId { get; set; }
    public Guid BattleId { get; set; }
}

// ===== 用户 DTOs =====
public sealed class UserInfoDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<UserCharacterDto> Characters { get; set; } = new();
}

public sealed class UserCharacterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public Profession Profession { get; set; }
    public int RosterOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}