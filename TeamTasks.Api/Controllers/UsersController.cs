using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TeamTasks.Application.Common;
using TeamTasks.Application.DTOs;
using TeamTasks.Infrastructure;

namespace TeamTasks.Api.Controllers;

[Authorize]
[ApiController]
[EnableRateLimiting("AuthPolicy")]
[Route("api/[controller]")]
public class UsersController(AppDbContext context) : ControllerBase
{
	[HttpGet("me")]
	[EndpointSummary("Gets the current user's data.")]
	[EndpointDescription("Contains the user's email and organizations")]
	public async Task<IActionResult> GetCurrentUser([FromServices] ITenantProvider tenantProvider) 
	{
		var orgs = await context.Organizations
			.IgnoreQueryFilters() 
			.Where(o => o.Users.Any(u => u.Id == tenantProvider.UserId))
			.Select(o => new OrganizationDto(o.Id, o.Name, null, null))
			.ToListAsync();
       
		return Ok(new { 
			Id = tenantProvider.UserId,
			Email = User.FindFirstValue(ClaimTypes.Email),
			Organizations = orgs,
			CurrentOrganizationId = tenantProvider.OrganizationId,
		});
	}
}