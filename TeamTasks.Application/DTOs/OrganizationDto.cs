namespace TeamTasks.Application.DTOs;

public record OrganizationDto(Guid Id, string Name, List<UserDto> Users, List<ProjectDto> Projects);