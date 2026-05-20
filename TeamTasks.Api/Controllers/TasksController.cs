using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamTasks.Application.Common;
using TeamTasks.Application.DTOs;
using TeamTasks.Application.UGC;
using TeamTasks.Domain;
using TeamTasks.Infrastructure;
using TaskStatus = TeamTasks.Domain.TaskStatus;

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
				t.Status,
				t.AssignedUserId
			))
			.FirstOrDefaultAsync();
		
		if (task == null)
			return NotFound("Task not found or you don't have access to it");

		return Ok(task);
	}
	
	[HttpPost("create/{projId:guid}")]
	public async Task<IActionResult> Create(Guid projId, [FromBody] CreateTaskRequest request, [FromServices] ITenantProvider tenantProvider)
	{
		await using var transaction = await context.Database.BeginTransactionAsync();

		try
		{
			var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projId);
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
				UnauthorizedAccessException => NotFound("Task not found or you don't have access to it"),
				_ => BadRequest()
			};
		}
	}
	
	[HttpPatch("{id:guid}")]
	public async Task<IActionResult> UpdateTask(Guid id, [FromBody] UpdateTaskRequest request, [FromServices] ITenantProvider tenantProvider)
	{
		await using var transaction = await context.Database.BeginTransactionAsync();

		try
		{
			var task = await context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
			if (task == null)
				throw new UnauthorizedAccessException();

			if (request.Title != null)
				task.Title = request.Title;
			if (request.Description != null)
				task.Description = request.Description;
			if (request.Status != null)
				task.Status = (TaskStatus)request.Status;


			await context.SaveChangesAsync();
			await transaction.CommitAsync();

			return Ok();
		}
		catch (Exception e)
		{
			await transaction.RollbackAsync();
			return e switch
			{
				UnauthorizedAccessException => NotFound("Task not found or you don't have access to it"),
				_ => BadRequest()
			};
		}
	}
	
	[HttpDelete("{id:guid}")]
	public async Task<IActionResult> DeleteTask(Guid id, [FromServices] ITenantProvider tenantProvider)
	{
		await using var transaction = await context.Database.BeginTransactionAsync();

		try
		{
			var task = await context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
			if (task == null)
				throw new UnauthorizedAccessException();

			var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == task.ProjectId);
			if (project == null)
				throw new UnauthorizedAccessException();
			
			context.Tasks.Remove(task);
			project.Tasks.Remove(task);
				
			await context.SaveChangesAsync();
			await transaction.CommitAsync();

			return Ok();
		}
		catch (Exception e)
		{
			await transaction.RollbackAsync();
			return e switch
			{
				UnauthorizedAccessException => NotFound("Task not found or you don't have access to it"),
				_ => BadRequest()
			};
		}
	}
}