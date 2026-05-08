using System.ComponentModel.DataAnnotations;

namespace TeamTasks.Domain;

public class User
{
	public Guid Id { get; set; }
	[Length(4, 16)]
	public string Name { get; set; }
	public string Email { get; set; }
	public string PasswordHash { get; set; }
	public Guid OrganizationId { get; set; }
	public Organization Organization { get; set; }
	public string? ExternalId { get; set; } // OAuth/OpenID
	public string? Provider { get; set; } // e.g. Google
}