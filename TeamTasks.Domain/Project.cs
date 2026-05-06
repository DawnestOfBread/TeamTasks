namespace TeamTasks.Domain;

public class Project
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public Guid OrganizationId { get; set; }
	public List<TaskItem> Tasks { get; set; }
}