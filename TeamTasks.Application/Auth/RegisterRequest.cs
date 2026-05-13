using System.ComponentModel.DataAnnotations;

namespace TeamTasks.Application.Auth;

public record RegisterRequest(string Email, string Password, [Length(4, 16)] string Username);