namespace TeamTasks.Application.UGC;

public record CreateTaskRequest(Guid ProjectId, string Title, string Description);