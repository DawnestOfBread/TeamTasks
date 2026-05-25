using MassTransit;
using TeamTasks.Application.Common.Interfaces;

namespace TeamTasks.Infrastructure.Messaging;

public class EventBus(IPublishEndpoint publishEndpoint) : IEventBus
{
	public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
	{
		await publishEndpoint.Publish(message, cancellationToken);
	}
}