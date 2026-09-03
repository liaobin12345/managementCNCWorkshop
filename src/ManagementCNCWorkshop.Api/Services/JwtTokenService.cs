using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ManagementCNCWorkshop.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace ManagementCNCWorkshop.Api.Services;

/// <summary>生成 JWT 访问令牌</summary>
public class JwtTokenService(IConfiguration config)
{
    public string CreateToken(Employee employee, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, employee.Id.ToString()),
            new(ClaimTypes.Name, employee.EmployeeNo),
            new(ClaimTypes.GivenName, employee.Name),
            new("WorkshopId", employee.WorkshopId.ToString())
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("缺少配置 Jwt:Secret");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expireMinutes = int.TryParse(config["Jwt:ExpireMinutes"], out var minutes) ? minutes : 720;

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
