namespace TeamTasks.Application.Common;

public interface ITenantProvider
{
	Guid? UserId { get; }
	Guid? OrganizationId { get; }
}