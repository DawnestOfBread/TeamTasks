using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamTasks.Application.Common;
using TeamTasks.Application.DTOs;
using TeamTasks.Domain;
using TeamTasks.Infrastructure;

namespace TeamTasks.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController(IJwtService jwtService, AppDbContext context) : ControllerBase
{
	[HttpGet("me")]
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