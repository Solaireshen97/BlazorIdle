using System.Net.Http.Json;
using BlazorIdle.Client.Config;
using Microsoft.JSInterop;

namespace BlazorIdle.Services;

/// <summary>
/// Service to load and provide battle UI configuration
/// </summary>
public class BattleUIConfigService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private BattleUIConfig? _config;
    private bool _isLoaded;

    public BattleUIConfigService(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Get the loaded configuration, loading it if necessary
    /// </summary>
    public async Task<BattleUIConfig> GetConfigAsync()
    {
        if (!_isLoaded)
        {
            await LoadConfigAsync();
        }
        return _config ?? new BattleUIConfig();
    }

    /// <summary>
    /// Load configuration from JSON file
    /// </summary>
    private async Task LoadConfigAsync()
    {
        try
        {
            _config = await _httpClient.GetFromJsonAsync<BattleUIConfig>("config/battle-ui-config.json");
            _isLoaded = true;
            
            Console.WriteLine($"Battle UI Config loaded: Cycling={_config?.ProgressBar.EnableCycling}, JIT={_config?.Polling.Adaptive.Enabled}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load battle UI config: {ex.Message}. Using defaults.");
            _config = new BattleUIConfig();
            _isLoaded = true;
        }
    }

    /// <summary>
    /// Reload configuration (useful for hot-reload scenarios)
    /// </summary>
    public async Task ReloadConfigAsync()
    {
        _isLoaded = false;
        await LoadConfigAsync();
    }
}
