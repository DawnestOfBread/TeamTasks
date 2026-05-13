using System.Security.Claims;
using TeamTasks.Application.Common;

namespace TeamTasks.Api.Services;

public class TenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
	public Guid? UserId {
		get {
			string? claim = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			return Guid.TryParse(claim, out var id) ? id : null;
		}
	}

	public Guid? OrganizationId {
		get {
			string? claim = httpContextAccessor.HttpContext?.User?.FindFirst("OrganizationId")?.Value;
			return Guid.TryParse(claim, out var id) && id != Guid.Empty ? id : null;
		}
	}
}