namespace BesCrmTest.Data.Entities;

// Дедлайн задачі. Зберігається колонками в таблиці Tasks (owned type),
// тому правило DueAt >= PlannedStartAt можна задати CHECK-обмеженням
public class Deadline
{
    public DateTime PlannedStartAt { get; set; }
    public DateTime DueAt { get; set; }
}
