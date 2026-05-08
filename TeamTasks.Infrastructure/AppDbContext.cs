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
		
		// Filtering
		modelBuilder.Entity<Project>()
			.HasQueryFilter(p => p.OrganizationId == tenantProvider.OrganizationId);
		modelBuilder.Entity<TaskItem>()
			.HasQueryFilter(t => t.OrganizationId == tenantProvider.OrganizationId);
		
		// Indexing
		modelBuilder.Entity<TaskItem>()
			.HasIndex(t => t.OrganizationId);
		modelBuilder.Entity<Project>()
			.HasIndex(p => p.OrganizationId);
		modelBuilder.Entity<User>()
			.HasIndex(u => u.Email)
			.IsUnique();
		modelBuilder.Entity<TaskItem>()
			.HasOne<Project>()
			.WithMany(p => p.Tasks)
			.HasForeignKey(t => t.ProjectId);
	}
}