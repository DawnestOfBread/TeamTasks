using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TeamTasks.Domain;

public class User
{
	public Guid Id { get; set; }
	[Length(4, 16)] public string Name { get; set; }
	public string Email { get; set; }
	[JsonIgnore] public string PasswordHash { get; set; }
	public ICollection<Organization> Organizations { get; set; } = new List<Organization>();
	[JsonIgnore] public string? ExternalId { get; set; } // OAuth/OpenID
	[JsonIgnore] public string? Provider { get; set; } // e.g. Google
}