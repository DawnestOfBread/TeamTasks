namespace TeamTasks.Application.DTOs;

public record ProjectDto(Guid Id, string Name, List<TaskDto> Tasks);