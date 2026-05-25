using TeamTasks.Application.DTOs;

namespace TeamTasks.Application.Common.Events;

public record TaskActivityEvent
{
	public TaskDto? Task { get; init; }
	public Guid ProjectId { get; init; }
	public Guid OrganizationId { get; init; }
	public string ActivityType { get; init; } = null!;
	public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}