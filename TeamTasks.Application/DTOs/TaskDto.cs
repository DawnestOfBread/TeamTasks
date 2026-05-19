namespace TeamTasks.Application.DTOs;

public record TaskDto(Guid Id, string Title, string Description, Guid? AssignedUser);