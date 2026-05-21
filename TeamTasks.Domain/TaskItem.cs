using System.ComponentModel.DataAnnotations;

namespace TeamTasks.Domain;

public class TaskItem
{
	public Guid Id { get; set; }
	[MaxLength(64)] public string Title { get; set; }
	[MaxLength(512)] public string Description { get; set; }
	public TaskStatus Status { get; set; }
	public Guid ProjectId { get; set; }
	public Guid? AssignedUserId { get; set; }
	public Guid OrganizationId { get; set; }
}