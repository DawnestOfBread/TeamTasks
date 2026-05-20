using TaskStatus = TeamTasks.Domain.TaskStatus;

namespace TeamTasks.Application.DTOs;

public record TaskDto(Guid Id, string Title, string Description, TaskStatus Status, Guid? AssignedUser);