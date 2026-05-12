using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TeamTasks.Application.Common;

namespace TeamTasks.Infrastructure.Security;

public class JwtService(IConfiguration configuration) : IJwtService
{
	public string GenerateToken(Guid userId, string email)
	{
		string secretKey = configuration["Jwt:SecretKey"] 
		                   ?? throw new InvalidOperationException("JWT Secret Key is missing.");
        
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		var claims = new Dictionary<string, object>
		{
			{ JwtRegisteredClaimNames.Sub, userId },
			{ JwtRegisteredClaimNames.Email, email },
			{ JwtRegisteredClaimNames.Jti,  Guid.NewGuid() }
		};

		var token = new SecurityTokenDescriptor
		{
			Issuer = configuration["Jwt:Issuer"],
			Audience = configuration["Jwt:Audience"],
			Claims = claims,
			Expires = DateTime.UtcNow.AddDays(7),
			SigningCredentials = creds
		};
		
		var handler = new JsonWebTokenHandler
			{
				SetDefaultTimesOnTokenCreation = false
			};

		return handler.CreateToken(token);
	}
}