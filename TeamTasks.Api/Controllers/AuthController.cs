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
			string passwordHash = passwordService.HashPassword(request.Password);
			var user = new User 
			{
				Id = Guid.NewGuid(),
				Email = request.Email,
				Name = request.Username,
				PasswordHash = passwordHash
			};
			context.Users.Add(user);

			await context.SaveChangesAsync();
			await transaction.CommitAsync();
			
			CreateToken(user, Guid.Empty);

			return Ok(new { 
				id = user.Id,
				email = user.Email
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
		var user = await context.Users.IgnoreQueryFilters().Include(user => user.Organizations).FirstOrDefaultAsync(u => u.Email == request.Email);
		if (user == null || !passwordService.VerifyPassword(request.Password, user.PasswordHash))
			return Unauthorized("Invalid email or password.");

		var orgId = Guid.Empty;
		if (user.Organizations.Count > 0)
			orgId = user.Organizations.First().Id;
		CreateToken(user, orgId);

		return Ok(new { 
			id = user.Id,
			email = user.Email
		});
	}

	[HttpGet("me")]
	[Authorize]
	public IActionResult GetCurrentUser([FromServices] ITenantProvider tenantProvider) 
	{
		return Ok(new { 
			email = User.FindFirstValue(ClaimTypes.Email),
			id = tenantProvider.UserId,
			orgId = tenantProvider.OrganizationId
		});
	}
	
	[HttpGet("login-google")]
	public IActionResult LoginGoogle()
	{
		var properties = new AuthenticationProperties { RedirectUri = "/" };
		return Challenge(properties, "Google");
	}
	
	[HttpPost("switch-org/{id}")]
	[Authorize]
	public async Task<IActionResult> SwitchOrg(Guid id)
	{
		var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
		
		var user = await context.Users.IgnoreQueryFilters()
			.FirstOrDefaultAsync(u => u.Id == userId && u.Organizations.Any(o => o.Id == id));

		if (user == null) return Forbid();
		
		CreateToken(user, id);

		return Ok();
	}
	
	
	private void CreateToken(User user, Guid orgId)
	{
		string token = jwtService.GenerateToken(user.Id, orgId, user.Email);

		AppendCookie(token);
	}
	private void AppendCookie(string newToken)
	{
		Response.Cookies.Append("X-Auth-Token", newToken, new CookieOptions
		{
			HttpOnly = true,
			Secure = false, // This should be enabled in prod
			SameSite = SameSiteMode.Lax,
			Expires = DateTime.UtcNow.AddDays(7)
		});
	}
}