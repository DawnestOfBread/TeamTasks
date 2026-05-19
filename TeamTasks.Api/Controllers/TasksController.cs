using Microsoft.AspNetCore.Authorization;
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
[Route("api/[controller]")]
public class TasksController(AppDbContext context) : ControllerBase
{
	[HttpGet("{id:guid}")]
	public async Task<IActionResult> GetById(Guid id, [FromServices] ITenantProvider tenantProvider)
	{
		var task = await context.Tasks
			.AsSplitQuery()
			.Where(t => t.Id == id) 
			.Select(t => new TaskDto(
				t.Id, 
				t.Title, 
				t.Description,
				t.AssignedUserId
			))
			.FirstOrDefaultAsync();
		
		if (task == null)
			return NotFound("Task not found or you don't have access to it");

		return Ok(task);
	}
	
	[HttpPost("create")]
	public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, [FromServices] ITenantProvider tenantProvider)
	{
		await using var transaction = await context.Database.BeginTransactionAsync();

		try
		{
			var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId);
			if (project == null)
				throw new UnauthorizedAccessException();
			
			var newTask = new TaskItem
			{
				Id = Guid.NewGuid(),
				Title = request.Title,
				Description = request.Description,
				OrganizationId = tenantProvider.OrganizationId ?? throw new UnauthorizedAccessException(),
				ProjectId = project.Id
			};
			context.Tasks.Add(newTask);
			project.Tasks.Add(newTask);
				
			await context.SaveChangesAsync();
			await transaction.CommitAsync();

			return CreatedAtAction(nameof(GetById), new { id = newTask.Id }, newTask);
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