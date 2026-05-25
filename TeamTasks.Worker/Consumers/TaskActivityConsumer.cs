using MassTransit;
using Microsoft.AspNetCore.SignalR;
using TeamTasks.Application.Common.Events;
using TeamTasks.Worker.Hubs;

namespace TeamTasks.Worker.Consumers;

public class TaskActivityConsumer(IHubContext<TaskActivityHub> hubContext) : IConsumer<TaskActivityEvent>
{
	public async Task Consume(ConsumeContext<TaskActivityEvent> context)
	{
		var message = context.Message;
		var targetTenantGroup = $"org_{message.OrganizationId}";
		
		await hubContext.Clients.Group(targetTenantGroup).SendAsync("ReceiveTaskUpdate", new
		{
			task = message.Task,
			projectId = message.ProjectId,
			activityType = message.ActivityType,
			timestamp = message.Timestamp
		});
	}
}