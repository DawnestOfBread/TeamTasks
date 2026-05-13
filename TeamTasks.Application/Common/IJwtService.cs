namespace TeamTasks.Application.Common;

public interface IJwtService
{
	string GenerateToken(Guid userId, Guid organizationId, string email);
}