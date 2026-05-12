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
public class OrganizationsController(AppDbContext context) : ControllerBase
{
	[HttpGet("{id:guid}")]
	public async Task<IActionResult> GetById(Guid id)
	{
		var org = await context.Organizations
			.AsSplitQuery()
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
}