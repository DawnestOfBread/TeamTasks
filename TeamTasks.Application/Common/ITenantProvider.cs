namespace TeamTasks.Application.Common;

public interface ITenantProvider
{
	Guid? OrganizationId { get; }
}