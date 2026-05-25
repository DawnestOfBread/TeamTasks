using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;
using TeamTasks.Application.Common;

namespace TeamTasks.Worker.Hubs;

[EnableCors]
[Authorize]
public class TaskActivityHub(ITenantProvider tenantProvider) : Hub
{
	public override async Task OnConnectedAsync()
	{
		var organizationId = tenantProvider.OrganizationId;

		if (organizationId != Guid.Empty)
		{
			var tenantGroup = $"org_{organizationId}";
			await Groups.AddToGroupAsync(Context.ConnectionId, tenantGroup);
		}
		else
			Context.Abort();

		await base.OnConnectedAsync();
	}

	public override async Task OnDisconnectedAsync(Exception? exception)
	{
		var organizationId = tenantProvider.OrganizationId;
        
		if (organizationId != Guid.Empty) 
			await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"org_{organizationId}");

		await base.OnDisconnectedAsync(exception);
	}
}