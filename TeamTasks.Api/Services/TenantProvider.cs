using TeamTasks.Application.Common;

namespace TeamTasks.Api.Services;

public class TenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
	public Guid? OrganizationId
	{
		get
		{
			string? claim = httpContextAccessor.HttpContext?.User?.FindFirst("OrganizationId")?.Value;
			return Guid.TryParse(claim, out var id) ? id : null;
		}
	}
}