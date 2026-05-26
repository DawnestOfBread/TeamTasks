namespace TeamTasks.Application.DTOs;

public record OrganizationDto(Guid Id, string Name, IEnumerable<UserDto>? Users, IEnumerable<ProjectDto>? Projects);