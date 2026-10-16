namespace BesCrmTest.Data.Entities;

public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public TaskStatusCode StatusId { get; set; } = TaskStatusCode.New;
    public TaskItemStatus Status { get; set; } = null!;

    public int CreatedById { get; set; }
    public Employee CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    // Заповнюється тільки для статусу Completed
    public DateTime? CompletedAt { get; set; }

    public Deadline Deadline { get; set; } = null!;

    public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
}
