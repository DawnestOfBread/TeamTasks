namespace TeamTasks.Domain;

public class TaskItem
{
	public Guid Id { get; set; }
	public string Title { get; set; }
	public TaskStatus Status { get; set; }
	public Guid? AssignedUserId { get; set; }
	public Guid ProjectId { get; set; }
}