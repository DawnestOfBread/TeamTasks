using System.ComponentModel.DataAnnotations;

namespace TeamTasks.Domain;

public class Organization
{
	public Guid Id { get; set; }
	[Length(4, 16)]
	public string Name { get; set; }
	public ICollection<User> Users { get; set; } = new List<User>();
	public ICollection<Project> Projects { get; set; } = new List<Project>();
}