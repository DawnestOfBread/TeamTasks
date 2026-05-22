using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
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
[EnableCors]
[Route("api/[controller]")]
public class OrganizationsController(AppDbContext context, ICacheService cache, IJwtService jwtService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        string cacheKey = $"org:{id}";
        var orgDto = await cache.GetAsync<OrganizationDto>(cacheKey);

        if (orgDto == null)
        {
            orgDto = await context.Organizations
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
                        p.Tasks.Select(t => new TaskDto(t.Id, t.Title, t.Description, t.Status, t.AssignedUserId)).ToList()
                    )).ToList()
                ))
                .FirstOrDefaultAsync();

            if (orgDto == null) 
                return NotFound("Organization not found or access denied.");

            await cache.SetAsync(cacheKey, orgDto, TimeSpan.FromMinutes(10));
        }

        return Ok(orgDto);
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
            if (user == null) return Unauthorized();
          
            var newOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = request.OrganizationName,
                Users = [user]
            };
            
            context.Organizations.Add(newOrg);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
          
            CreateToken(user, newOrg.Id);
            return Ok(new { id = newOrg.Id });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return BadRequest("Failed to create organization");
        }
    }
    
    [HttpPost("invite")]
    public async Task<IActionResult> InviteUser([FromBody] InviteToOrganizationRequest request, [FromServices] ITenantProvider tenantProvider)
    {
        if (tenantProvider.OrganizationId == null) return BadRequest("Active tenant organization context missing.");

        await using var transaction = await context.Database.BeginTransactionAsync();
        try 
        {
            var organization = await context.Organizations
                .Include(o => o.Users)
                .FirstOrDefaultAsync(o => o.Id == tenantProvider.OrganizationId);

            if (organization == null) return NotFound("Organization context not found.");
          
            var user = await context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || organization.Users.Any(u => u.Id == user.Id))
                return BadRequest("User not found or already a member of this organization.");
          
            organization.Users.Add(user);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
          
            await cache.RemoveAsync($"org:{tenantProvider.OrganizationId}");
            return Ok(new UserDto(user.Id, user.Name));
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return BadRequest("Invitation processing error.");
        }
    }
    
    private void CreateToken(User user, Guid orgId)
    {
        string token = jwtService.GenerateToken(user.Id, orgId, user.Email);
        Response.Cookies.Append("X-Auth-Token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(7)
        });
    }
}