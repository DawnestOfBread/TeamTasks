using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamTasks.Application.Common;
using TeamTasks.Application.UGC;
using TeamTasks.Domain;
using TeamTasks.Infrastructure;

namespace TeamTasks.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProjectsController(AppDbContext context) : ControllerBase
{
	[HttpGet]
	public async Task<IActionResult> List()
	{
		var projects = await context.Projects
			.Include(p => p.Tasks) 
			.ToListAsync();

		return Ok(projects);
	}
	
	[HttpGet("{id:guid}")]
	public async Task<IActionResult> GetById(Guid id)
	{
		var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == id);

		if (project == null) 
			return NotFound("Project not found or you don't have access to it");

		return Ok(project);
	}
	
	[HttpPost("create")]
	public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, [FromServices] ITenantProvider tenantProvider)
	{
		await using var transaction = await context.Database.BeginTransactionAsync();

		try
		{
			var newProject = new Project
			{
				Id = Guid.NewGuid(),
				Name = request.Name,
				OrganizationId = tenantProvider.OrganizationId ?? throw new UnauthorizedAccessException()
			};
			context.Projects.Add(newProject);

			await context.SaveChangesAsync();
			await transaction.CommitAsync();

			return CreatedAtAction(nameof(GetById), new { id = newProject.Id }, newProject);
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
}