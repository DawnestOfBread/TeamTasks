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
        
        modelBuilder.Entity<Organization>()
            .HasQueryFilter(o => o.Users.Any(u => u.Id == currentUserId));
        modelBuilder.Entity<Project>()
            .HasQueryFilter(p => p.OrganizationId == currentOrgId);
        modelBuilder.Entity<TaskItem>()
            .HasQueryFilter(t => t.OrganizationId == currentOrgId);
        
        modelBuilder.Entity<User>()
            .HasQueryFilter(u => !currentUserId.HasValue || u.Id == currentUserId || u.Organizations.Any(o => o.Id == currentOrgId));
        
        modelBuilder.Entity<User>()
            .HasMany(u => u.Organizations)
            .WithMany(o => o.Users)
            .UsingEntity<Dictionary<string, object>>(
                "UserOrganizations",
                j => j.HasOne<Organization>().WithMany().HasForeignKey("OrganizationId"),
                j => j.HasOne<User>().WithMany().HasForeignKey("UserId"),
                j =>
                {
                    j.ToTable("UserOrganizations");
                    j.HasKey("UserId", "OrganizationId");
                });
        
        modelBuilder.Entity<Project>()
            .HasMany(p => p.Tasks)
            .WithOne()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Project>().HasIndex(p => p.OrganizationId);
        modelBuilder.Entity<TaskItem>().HasIndex(t => t.OrganizationId);
        
        modelBuilder.Entity<TaskItem>().HasIndex(t => t.ProjectId);
        
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        
        modelBuilder.Entity<User>()
            .HasIndex(u => new { u.Provider, u.ExternalId })
            .HasFilter("\"ExternalId\" IS NOT NULL");
    }
}