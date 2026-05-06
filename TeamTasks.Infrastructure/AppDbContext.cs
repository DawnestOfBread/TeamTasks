using Microsoft.EntityFrameworkCore;
using TeamTasks.Domain;

namespace TeamTasks.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
	public DbSet<Organization> Organizations { get; set; }
	public DbSet<User> Users { get; set; }
	public DbSet<Project> Projects { get; set; }
	public DbSet<TaskItem> Tasks { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		
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