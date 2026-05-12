namespace TeamTasks.Application.Common;

public interface IJwtService
{
	string GenerateToken(Guid userId, string email);
}