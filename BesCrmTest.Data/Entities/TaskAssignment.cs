namespace BesCrmTest.Data.Entities;

public class TaskAssignment
{
    public int Id { get; set; }

    public int TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;

    // Виконавець
    public int AssigneeId { get; set; }
    public Employee Assignee { get; set; } = null!;

    // Хто призначив (не може збігатися з виконавцем)
    public int AssignedById { get; set; }
    public Employee AssignedBy { get; set; } = null!;

    public DateTime AssignedAt { get; set; }
}
