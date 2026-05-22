using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TeamTasks.Application.Cache;
using TeamTasks.Application.Common;
using TeamTasks.Application.DTOs;
using TeamTasks.Application.UGC;
using TeamTasks.Domain;
using TeamTasks.Infrastructure;

namespace TeamTasks.Api.Controllers;

[Authorize]
[ApiController]
[EnableRateLimiting("TenantPolicy")]
[Route("api/[controller]")]
public class ProjectsController(AppDbContext context, ICacheService cache) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        // Global list endpoint strictly falls back to pure database query tracking 
        var projects = await context.Projects.Include(p => p.Tasks).ToListAsync();
        return Ok(projects);
    }
    
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        string cacheKey = $"project:{id}";
        var projectDto = await cache.GetAsync<ProjectDto>(cacheKey);

        if (projectDto == null)
        {
            projectDto = await context.Projects
                .AsSplitQuery()
                .Where(p => p.Id == id) 
                .Select(p => new ProjectDto(
                    p.Id, 
                    p.Name, 
                    p.Tasks.Select(t => new TaskDto(t.Id, t.Title, t.Description, t.Status, t.AssignedUserId)).ToList()
                ))
                .FirstOrDefaultAsync();
       
            if (projectDto == null)
                return NotFound("Project not found or access denied.");

            await cache.SetAsync(cacheKey, projectDto, TimeSpan.FromMinutes(10));
        }

        return Ok(projectDto);
    }
    
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, [FromServices] ITenantProvider tenantProvider)
    {
        if (tenantProvider.OrganizationId == null) return BadRequest("Active tenant missing.");

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var organization = await context.Organizations.FirstOrDefaultAsync(o => o.Id == tenantProvider.OrganizationId);
            if (organization == null) return Unauthorized();
          
            var newProject = new Project
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                OrganizationId = organization.Id
            };

            context.Projects.Add(newProject);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            await cache.RemoveAsync($"org:{tenantProvider.OrganizationId}");

            return CreatedAtAction(nameof(GetById), new { id = newProject.Id }, newProject);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return BadRequest("Project registration dropped.");
        }
    }
}