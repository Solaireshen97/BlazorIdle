using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BlazorIdle.Server.Application.Auth;

/// <summary>
/// JWT Token 生成和验证服务
/// </summary>
public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// 为用户生成 JWT Token
    /// </summary>
    public string GenerateToken(Guid userId, string username, string email)
    {
        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                double.Parse(_configuration["Jwt:ExpirationMinutes"]!)),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// 从 ClaimsPrincipal 提取用户 ID
    /// </summary>
    public static Guid? GetUserIdFromClaims(ClaimsPrincipal principal)
    {
        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier) 
                       ?? principal.FindFirst(JwtRegisteredClaimNames.Sub);
        
        if (subClaim != null && Guid.TryParse(subClaim.Value, out var userId))
        {
            return userId;
        }
        
        return null;
    }
}
