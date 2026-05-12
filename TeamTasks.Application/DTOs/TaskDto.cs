namespace TeamTasks.Application.DTOs;

public record TaskDto(Guid Id, string Name, string Description, Guid? AssignedUser);