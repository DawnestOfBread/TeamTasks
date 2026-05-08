using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TeamTasks.Application.Common;

namespace TeamTasks.Infrastructure;

/// <summary>
/// Dummy class to make migrations work
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
	public AppDbContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
		
		optionsBuilder.UseNpgsql("Server=localhost;Database=MigrationOnly;Integrated Security=SSPI;TrustServerCertificate=True");
		return new AppDbContext(optionsBuilder.Options, new DesignTimeTenantProvider());
	}
}

internal class DesignTimeTenantProvider : ITenantProvider
{
	public Guid? OrganizationId => Guid.Empty;
}