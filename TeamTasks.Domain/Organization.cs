using System.ComponentModel.DataAnnotations;

namespace TeamTasks.Domain;

public class Organization
{
	public Guid Id { get; set; }
	[Length(4, 16)]
	public string Name { get; set; }
	public List<User> Users { get; set; }
	public List<Project> Projects { get; set; }
}