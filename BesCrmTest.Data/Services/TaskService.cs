using BesCrmTest.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BesCrmTest.Data.Services;

// CRUD-сценарії модуля задач і перевірки бізнес-правил.
// Частину правил продубльовано CHECK-обмеженнями та тригерами в БД (див. Configurations, Migrations),
// тут вони перевіряються заздалегідь, щоб повернути зрозумілу помилку, а не виняток PostgreSQL.
public class TaskService(CrmDbContext db)
{
    // Створити задачу. Нова задача завжди має статус New.
    public async Task<TaskItem> CreateTaskAsync(
        string title,
        string? description,
        int createdById,
        DateTime plannedStartAt,
        DateTime dueAt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new BusinessRuleException("Task title is required.");

        plannedStartAt = ToUtc(plannedStartAt);
        dueAt = ToUtc(dueAt);

        // Правило: DueAt не може бути раніше за PlannedStartAt
        if (dueAt < plannedStartAt)
            throw new BusinessRuleException("DueAt cannot be earlier than PlannedStartAt.");

        if (!await db.Employees.AnyAsync(e => e.Id == createdById, cancellationToken))
            throw new EntityNotFoundException(nameof(Employee), createdById);

        var task = new TaskItem
        {
            Title = title.Trim(),
            Description = description,
            StatusId = TaskStatusCode.New,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow,
            Deadline = new Deadline { PlannedStartAt = plannedStartAt, DueAt = dueAt }
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);

        return task;
    }

    // Призначити задачу виконавцю.
    public async Task<TaskAssignment> AssignTaskAsync(
        int taskId,
        int assigneeId,
        int assignedById,
        CancellationToken cancellationToken = default)
    {
        // Правило: задача не може бути призначена самому собі
        if (assigneeId == assignedById)
            throw new BusinessRuleException("A task cannot be assigned to yourself.");

        if (!await db.Tasks.AnyAsync(t => t.Id == taskId, cancellationToken))
            throw new EntityNotFoundException(nameof(TaskItem), taskId);

        var assignee = await db.Employees.FindAsync([assigneeId], cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Employee), assigneeId);

        if (!await db.Employees.AnyAsync(e => e.Id == assignedById, cancellationToken))
            throw new EntityNotFoundException(nameof(Employee), assignedById);

        // Правило: неактивному співробітнику не можна призначити нову задачу
        if (!assignee.IsActive)
            throw new BusinessRuleException($"Employee {assigneeId} is inactive and cannot be assigned a new task.");

        var alreadyAssigned = await db.TaskAssignments
            .AnyAsync(a => a.TaskId == taskId && a.AssigneeId == assigneeId, cancellationToken);
        if (alreadyAssigned)
            throw new BusinessRuleException($"Task {taskId} is already assigned to employee {assigneeId}.");

        var assignment = new TaskAssignment
        {
            TaskId = taskId,
            AssigneeId = assigneeId,
            AssignedById = assignedById,
            AssignedAt = DateTime.UtcNow
        };

        db.TaskAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);

        return assignment;
    }

    // Змінити статус задачі.
    public async Task<TaskItem> ChangeStatusAsync(
        int taskId,
        TaskStatusCode newStatus,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(newStatus))
            throw new BusinessRuleException($"Unknown status {(int)newStatus}.");

        var task = await db.Tasks
            .Include(t => t.Status)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskItem), taskId);

        if (task.StatusId == newStatus)
            return task;

        // Правило: Completed і Cancelled — фінальні статуси, з них не можна перейти в інший
        if (task.Status.IsFinal)
            throw new BusinessRuleException(
                $"Task {taskId} is in final status '{task.Status.Name}' and its status cannot be changed.");

        task.StatusId = newStatus;

        // Правило: CompletedAt заповнюється тільки для статусу Completed
        task.CompletedAt = newStatus == TaskStatusCode.Completed ? DateTime.UtcNow : null;

        await db.SaveChangesAsync(cancellationToken);

        // Довантажуємо новий статус, щоб повернути актуальну навігацію
        await db.Entry(task).Reference(t => t.Status).LoadAsync(cancellationToken);

        return task;
    }

    // Отримати список задач за виконавцем (найближчий дедлайн — першим).
    public async Task<List<TaskItem>> GetTasksByAssigneeAsync(
        int assigneeId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == assigneeId, cancellationToken))
            throw new EntityNotFoundException(nameof(Employee), assigneeId);

        return await db.Tasks
            .AsNoTracking()
            .Include(t => t.Status)
            .Include(t => t.CreatedBy)
            .Where(t => t.Assignments.Any(a => a.AssigneeId == assigneeId))
            .OrderBy(t => t.Deadline.DueAt)
            .ToListAsync(cancellationToken);
    }

    // Npgsql записує в timestamptz тільки DateTime з Kind = Utc.
    // Дати без вказаного Kind вважаємо вже переданими в UTC.
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
