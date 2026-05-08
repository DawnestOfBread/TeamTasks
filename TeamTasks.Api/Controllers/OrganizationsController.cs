using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamTasks.Application.Common;
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
			.Include(o => o.Projects)
			.FirstOrDefaultAsync(o => o.Id == id);

		if (org == null) 
			return NotFound("Organization not found or you don't have access to it");

		return Ok(org);
	}
}