namespace BesCrmTest.Data.Entities;

public class Employee
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Position { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    // Задачі, створені співробітником (автор)
    public ICollection<TaskItem> CreatedTasks { get; set; } = new List<TaskItem>();

    // Призначення, де співробітник — виконавець
    public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();

    // Призначення, які співробітник видав іншим
    public ICollection<TaskAssignment> GivenAssignments { get; set; } = new List<TaskAssignment>();
}
