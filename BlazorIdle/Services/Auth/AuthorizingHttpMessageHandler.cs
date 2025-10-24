using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Services.Auth;

/// <summary>
/// HTTP消息处理器 - 自动附加JWT Token到HTTP请求
/// 实现了DelegatingHandler，可以拦截所有HTTP请求并在发送前附加认证头
/// </summary>
public class AuthorizingHttpMessageHandler : DelegatingHandler
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthorizingHttpMessageHandler> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="authService">认证服务（用于获取Token）</param>
    /// <param name="logger">日志服务</param>
    public AuthorizingHttpMessageHandler(
        IAuthenticationService authService,
        ILogger<AuthorizingHttpMessageHandler> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// 发送HTTP请求前的拦截处理
    /// 自动附加JWT Token到Authorization头
    /// </summary>
    /// <param name="request">HTTP请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>HTTP响应</returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 获取当前存储的Token
        var token = await _authService.GetTokenAsync();

        // 如果Token存在，附加到请求头
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _logger.LogDebug("已附加JWT Token到请求：{Method} {Uri}", request.Method, request.RequestUri);
        }

        // 发送请求
        var response = await base.SendAsync(request, cancellationToken);

        // 如果收到401未授权响应，可能需要刷新Token
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("收到401未授权响应：{Method} {Uri}", request.Method, request.RequestUri);

            // 检查响应头是否标记Token已过期
            if (response.Headers.Contains("Token-Expired"))
            {
                _logger.LogInformation("Token已过期，尝试刷新");
                
                // 尝试刷新Token
                var refreshResult = await _authService.RefreshTokenAsync();
                
                if (refreshResult.Success)
                {
                    _logger.LogInformation("Token刷新成功，重试原请求");
                    
                    // 获取新Token并重试原请求
                    var newToken = await _authService.GetTokenAsync();
                    if (!string.IsNullOrEmpty(newToken))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                        response = await base.SendAsync(request, cancellationToken);
                    }
                }
                else
                {
                    _logger.LogWarning("Token刷新失败：{Message}", refreshResult.Message);
                }
            }
        }

        return response;
    }
}
