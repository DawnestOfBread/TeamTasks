using TaskStatus = TeamTasks.Domain.TaskStatus;

namespace TeamTasks.Application.UGC;

public record UpdateTaskRequest(string? Title, string? Description, TaskStatus? Status, Guid? AssignedUser);