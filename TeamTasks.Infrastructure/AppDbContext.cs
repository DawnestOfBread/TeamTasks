using Microsoft.EntityFrameworkCore;
using TeamTasks.Application.Common;
using TeamTasks.Domain;

namespace TeamTasks.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) : DbContext(options)
{
	public DbSet<Organization> Organizations { get; set; }
	public DbSet<User> Users { get; set; }
	public DbSet<Project> Projects { get; set; }
	public DbSet<TaskItem> Tasks { get; set; }
	
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
    
		var currentOrgId = tenantProvider.OrganizationId;
		var currentUserId = tenantProvider.UserId;
		
		modelBuilder.Entity<Project>()
			.HasQueryFilter(p => p.OrganizationId == currentOrgId);
       
		modelBuilder.Entity<TaskItem>()
			.HasQueryFilter(t => t.OrganizationId == currentOrgId);
		
		modelBuilder.Entity<Organization>()
			.HasQueryFilter(o => o.Users.Any(u => u.Id == currentUserId));
		modelBuilder.Entity<User>()
			.HasQueryFilter(u => 
				currentOrgId == null || 
				u.Organizations.Any(o => o.Id == currentOrgId)
			);
		
		modelBuilder.Entity<TaskItem>().HasIndex(t => t.OrganizationId);
		modelBuilder.Entity<Project>().HasIndex(p => p.OrganizationId);
		modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
		
		modelBuilder.Entity<User>()
			.HasMany(u => u.Organizations)
			.WithMany(o => o.Users)
			.UsingEntity(j => j.ToTable("UserOrganizations"));
		modelBuilder.Entity<TaskItem>()
			.HasOne<Project>()
			.WithMany(p => p.Tasks)
			.HasForeignKey(t => t.ProjectId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}