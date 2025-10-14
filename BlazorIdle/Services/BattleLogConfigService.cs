using System.Net.Http.Json;
using BlazorIdle.Models;

namespace BlazorIdle.Services;

/// <summary>
/// 战斗日志配置服务
/// </summary>
public sealed class BattleLogConfigService
{
    private readonly HttpClient _http;
    private BattleLogConfig? _config;
    private bool _isLoaded;

    public BattleLogConfigService(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// 获取战斗日志配置
    /// </summary>
    public async Task<BattleLogConfig> GetConfigAsync()
    {
        if (_isLoaded && _config != null)
        {
            return _config;
        }

        try
        {
            _config = await _http.GetFromJsonAsync<BattleLogConfig>("config/battle-log-config.json");
            _isLoaded = true;
            return _config ?? new BattleLogConfig();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BattleLogConfig] 加载配置失败: {ex.Message}，使用默认配置");
            _config = new BattleLogConfig();
            _isLoaded = true;
            return _config;
        }
    }

    /// <summary>
    /// 重新加载配置
    /// </summary>
    public async Task ReloadAsync()
    {
        _isLoaded = false;
        _config = null;
        await GetConfigAsync();
    }
}
