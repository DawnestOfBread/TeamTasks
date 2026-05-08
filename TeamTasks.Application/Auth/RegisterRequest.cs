namespace TeamTasks.Application.Auth;

public record RegisterRequest(string Email, string Password, string Username, string OrganizationName);