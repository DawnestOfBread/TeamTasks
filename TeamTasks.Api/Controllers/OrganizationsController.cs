using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamTasks.Application.Common;
using TeamTasks.Application.DTOs;
using TeamTasks.Application.UGC;
using TeamTasks.Domain;
using TeamTasks.Infrastructure;

namespace TeamTasks.Api.Controllers;

[Authorize]
[ApiController]
[EnableCors]
[Route("api/[controller]")]
public class OrganizationsController(AppDbContext context, IJwtService jwtService) : ControllerBase
{
	[HttpGet("{id:guid}")]
	public async Task<IActionResult> GetById(Guid id)
	{
		var org = await context.Organizations
			.AsSplitQuery()
			.IgnoreQueryFilters()
			.Where(o => o.Id == id)
			.Select(o => new OrganizationDto(
				o.Id,
				o.Name,
				o.Users.Select(u => new UserDto(u.Id, u.Name)).ToList(),
				o.Projects.Select(p => new ProjectDto(
					p.Id,
					p.Name,
					p.Tasks.Select(t => new TaskDto(t.Id, t.Title, t.Description, t.AssignedUserId)).ToList()
				)).ToList()
			))
			.FirstOrDefaultAsync();

		if (org == null) 
			return NotFound("Organization not found or you don't have access to it");

		return Ok(org);
	}
	
	[HttpGet]
	public async Task<IActionResult> GetAll([FromServices] ITenantProvider tenantProvider)
	{
		var orgs = await context.Organizations
			.IgnoreQueryFilters() 
			.AsSplitQuery()
			.Where(o => o.Users.Any(u => u.Id == tenantProvider.UserId))
			.Select(o => new OrganizationDto(
				o.Id,
				o.Name,
				o.Users.Select(u => new UserDto(u.Id, u.Name)).ToList(),
				o.Projects.Select(p => new ProjectDto(p.Id, p.Name, new List<TaskDto>())).ToList()
			))
			.ToListAsync();

		return Ok(orgs);
	}
	
	[HttpPost("create")]
	public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request, [FromServices] ITenantProvider tenantProvider)
	{
		await using var transaction = await context.Database.BeginTransactionAsync();

		try 
		{
			var user = await context.Users.FirstOrDefaultAsync(u => u.Id == tenantProvider.UserId);
			if (user == null)
				throw new UnauthorizedAccessException();
			
			var newOrg = new Organization
			{
				Id = Guid.NewGuid(),
				Name = request.OrganizationName,
				Users = [user]
			};
			context.Organizations.Add(newOrg);
			user.Organizations.Add(newOrg);

			await context.SaveChangesAsync();
			await transaction.CommitAsync();

			CreateToken(user, newOrg.Id);
			
			return Ok(new
			{
				id = newOrg.Id
			});
		}
		catch (Exception e)
		{
			await transaction.RollbackAsync();
			return e switch
			{
				UnauthorizedAccessException => Unauthorized(),
				_ => BadRequest()
			};
		}
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