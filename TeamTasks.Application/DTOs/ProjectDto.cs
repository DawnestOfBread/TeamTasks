namespace TeamTasks.Application.DTOs;

public record ProjectDto(Guid Id, string Name, IEnumerable<TaskDto> Tasks);