using System.ComponentModel.DataAnnotations;

namespace TeamTasks.Domain;

public class Project
{
	public Guid Id { get; set; }
	[MaxLength(64)]
	public string Name { get; set; }
	public Guid OrganizationId { get; set; }
	public List<TaskItem> Tasks { get; set; }
}