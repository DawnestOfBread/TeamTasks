using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamTasks.Application.Auth;
using TeamTasks.Application.Common;
using TeamTasks.Domain;
using TeamTasks.Infrastructure;
using TeamTasks.Infrastructure.Security;
using RegisterRequest = TeamTasks.Application.Auth.RegisterRequest;

namespace TeamTasks.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IPasswordService passwordService, IJwtService jwtService, AppDbContext context) : ControllerBase
{
	[HttpPost("register")]
	public async Task<IActionResult> Register([FromBody] RegisterRequest request)
	{
		await using var transaction = await context.Database.BeginTransactionAsync();

		try 
		{
			var org = new Organization { Id = Guid.NewGuid(), Name = request.OrganizationName };
			context.Organizations.Add(org);

			string passwordHash = passwordService.HashPassword(request.Password);
			var user = new User 
			{
				Id = Guid.NewGuid(),
				Email = request.Email,
				Name = request.Username,
				PasswordHash = passwordHash,
				OrganizationId = org.Id
			};
			context.Users.Add(user);

			await context.SaveChangesAsync();
			await transaction.CommitAsync();
			
			CreateToken(user);

			return Ok(new { 
				email = user.Email,
				orgId = user.OrganizationId
			});
		}
		catch (Exception)
		{
			await transaction.RollbackAsync();
			return BadRequest("Registration failed");
		}
	}
	
	[HttpPost("login")]
	public async Task<IActionResult> Login([FromBody] LoginRequest request)
	{
		var user = await context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
		if (user == null || !passwordService.VerifyPassword(request.Password, user.PasswordHash))
			return Unauthorized("Invalid email or password.");
		
		CreateToken(user);

		return Ok(new { 
			email = user.Email,
			orgId = user.OrganizationId
		});
	}

	private void CreateToken(User user)
	{
		string token = jwtService.GenerateToken(user.Id, user.OrganizationId, user.Email);

		Response.Cookies.Append("X-Auth-Token", token, new CookieOptions
		{
			HttpOnly = true,
			Secure = false, // This should be enabled in prod
			SameSite = SameSiteMode.Lax,
			Expires = DateTime.UtcNow.AddDays(7)
		});
	}

	[HttpGet("me")]
	[Authorize]
	public IActionResult GetCurrentUser() 
	{
		return Ok(new { 
			email = User.FindFirstValue(ClaimTypes.Email),
			orgId = User.FindFirstValue("OrganizationId")
		});
	}
	
	[HttpGet("login-google")]
	public IActionResult LoginGoogle()
	{
		var properties = new AuthenticationProperties { RedirectUri = "/" };
		return Challenge(properties, "Google");
	}
}