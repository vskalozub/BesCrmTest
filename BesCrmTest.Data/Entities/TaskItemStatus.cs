namespace BesCrmTest.Data.Entities;

// Значення збігаються з Id у таблиці TaskStatuses
public enum TaskStatusCode
{
    New = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

// Довідник статусів
public class TaskItemStatus
{
    public TaskStatusCode Id { get; set; }
    public string Name { get; set; } = null!;

    // Фінальний статус: з нього не можна перейти в інший
    public bool IsFinal { get; set; }

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
