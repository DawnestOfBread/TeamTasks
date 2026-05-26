using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TeamTasks.Application.Cache;
using TeamTasks.Application.Common;
using TeamTasks.Application.Common.Events;
using TeamTasks.Application.Common.Interfaces;
using TeamTasks.Application.DTOs;
using TeamTasks.Application.UGC;
using TeamTasks.Domain;
using TeamTasks.Infrastructure;
using TaskStatus = TeamTasks.Domain.TaskStatus;

namespace TeamTasks.Api.Controllers;

[Authorize]
[ApiController]
[EnableRateLimiting("TenantPolicy")]
[Route("api/[controller]")]
public class TasksController(AppDbContext context, ICacheService cache, IEventBus eventBus) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        string cacheKey = $"task:{id}";
        var taskDto = await cache.GetAsync<TaskDto>(cacheKey);
        if (taskDto != null) return Ok(taskDto);

        taskDto = await context.Tasks
            .Where(t => t.Id == id) 
            .Select(t => new TaskDto(t.Id, t.Title, t.Description, t.Status, t.AssignedUserId))
            .FirstOrDefaultAsync();
   
        if (taskDto == null)
            return NotFound("Task not found or access denied.");

        await cache.SetAsync(cacheKey, taskDto, TimeSpan.FromMinutes(10));
        return Ok(taskDto);
    }
    
    [HttpPost("create/{projId:guid}")]
    public async Task<IActionResult> Create(Guid projId, [FromBody] CreateTaskRequest request, [FromServices] ITenantProvider tenantProvider)
    {
        if (tenantProvider.OrganizationId == null) return Unauthorized();

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projId);
            if (project == null) return NotFound("Project target missing.");
          
            var newTask = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Description = request.Description,
                OrganizationId = tenantProvider.OrganizationId.Value,
                ProjectId = project.Id,
                Status = TaskStatus.Todo
            };

            context.Tasks.Add(newTask);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            await cache.RemoveAsync($"project:{project.Id}");
            await cache.RemoveAsync($"org:{tenantProvider.OrganizationId}");
            
            await eventBus.PublishAsync(new TaskActivityEvent
            {
                Task = new TaskDto(newTask.Id, newTask.Title, newTask.Description, newTask.Status, newTask.AssignedUserId),
                ProjectId = newTask.ProjectId,
                OrganizationId = tenantProvider.OrganizationId.Value,
                ActivityType = "Create",
            });

            return CreatedAtAction(nameof(GetById), new { id = newTask.Id }, newTask);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return BadRequest("Task tracking generation rejected.");
        }
    }
    
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateTask(Guid id, [FromBody] UpdateTaskRequest request, [FromServices] ITenantProvider tenantProvider)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var task = await context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (task == null) return NotFound("Task context dropped.");

            if (request.Title != null) task.Title = request.Title;
            if (request.Description != null) task.Description = request.Description;
            if (request.Status != null) task.Status = (TaskStatus)request.Status;
            task.AssignedUserId = request.AssignedUser;

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            await cache.RemoveAsync($"task:{id}");
            await cache.RemoveAsync($"project:{task.ProjectId}");
            await cache.RemoveAsync($"org:{tenantProvider.OrganizationId}");
            
            await eventBus.PublishAsync(new TaskActivityEvent
            {
                Task = new TaskDto(task.Id, task.Title, task.Description, task.Status, task.AssignedUserId),
                ProjectId = task.ProjectId,
                OrganizationId = tenantProvider.OrganizationId!.Value,
                ActivityType = "Update",
            });

            return Ok(task);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return BadRequest("Patch mutation rejected.");
        }
    }
    
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id, [FromServices] ITenantProvider tenantProvider)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var task = await context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
            if (task == null) return NotFound("Task not found.");
          
            context.Tasks.Remove(task);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
          
            await cache.RemoveAsync($"task:{id}");
            await cache.RemoveAsync($"project:{task.ProjectId}");
            await cache.RemoveAsync($"org:{tenantProvider.OrganizationId}");
          
            await eventBus.PublishAsync(new TaskActivityEvent
            {
                Task = new TaskDto(task.Id, task.Title, task.Description, task.Status, task.AssignedUserId),
                ProjectId = task.ProjectId,
                OrganizationId = tenantProvider.OrganizationId!.Value,
                ActivityType = "Delete",
            });
            
            return Ok();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return BadRequest("Deletion processing rejected.");
        }
    }
}