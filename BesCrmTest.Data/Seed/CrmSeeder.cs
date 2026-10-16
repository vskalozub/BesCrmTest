using BesCrmTest.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BesCrmTest.Data.Seed;

// Тестові дані: співробітники, задачі, призначення.
// Виконується після застосування міграцій (Migrate / dotnet ef database update).
// Статуси сюди не входять — вони в міграції через HasData (TaskItemStatusConfiguration).
public static class CrmSeeder
{
    public static DbContextOptionsBuilder UseCrmSeeding(this DbContextOptionsBuilder options) =>
        options
            .UseSeeding((context, _) =>
            {
                var db = (CrmDbContext)context;
                if (db.Employees.Any())
                    return;

                AddTestData(db);
                db.SaveChanges();
            })
            .UseAsyncSeeding(async (context, _, cancellationToken) =>
            {
                var db = (CrmDbContext)context;
                if (await db.Employees.AnyAsync(cancellationToken))
                    return;

                AddTestData(db);
                await db.SaveChangesAsync(cancellationToken);
            });

    private static void AddTestData(CrmDbContext db)
    {
        var now = DateTime.UtcNow;

        var lead = new Employee
        {
            FullName = "Іван Петренко",
            Email = "ivan.petrenko@bescrm.local",
            Position = "Team Lead",
            IsActive = true,
            CreatedAt = now
        };

        var developer = new Employee
        {
            FullName = "Олена Коваль",
            Email = "olena.koval@bescrm.local",
            Position = "Developer",
            IsActive = true,
            CreatedAt = now
        };

        // Неактивний співробітник — для перевірки правила "не можна призначити нову задачу"
        var formerQa = new Employee
        {
            FullName = "Андрій Шевченко",
            Email = "andrii.shevchenko@bescrm.local",
            Position = "QA Engineer",
            IsActive = false,
            CreatedAt = now
        };

        db.Employees.AddRange(lead, developer, formerQa);

        db.Tasks.AddRange(
            new TaskItem
            {
                Title = "Налаштувати CI для репозиторію",
                Description = "Збірка та запуск тестів на кожен push",
                StatusId = TaskStatusCode.New,
                CreatedBy = lead,
                CreatedAt = now,
                Deadline = new Deadline { PlannedStartAt = now.AddDays(1), DueAt = now.AddDays(5) },
                Assignments =
                {
                    new TaskAssignment { Assignee = developer, AssignedBy = lead, AssignedAt = now }
                }
            },
            new TaskItem
            {
                Title = "Підготувати звіт про продажі",
                StatusId = TaskStatusCode.InProgress,
                CreatedBy = lead,
                CreatedAt = now.AddDays(-3),
                Deadline = new Deadline { PlannedStartAt = now.AddDays(-2), DueAt = now.AddDays(2) },
                Assignments =
                {
                    new TaskAssignment { Assignee = developer, AssignedBy = lead, AssignedAt = now.AddDays(-3) }
                }
            },
            new TaskItem
            {
                Title = "Code review модуля завдань",
                StatusId = TaskStatusCode.Completed,
                CreatedBy = developer,
                CreatedAt = now.AddDays(-7),
                CompletedAt = now.AddDays(-4),
                Deadline = new Deadline { PlannedStartAt = now.AddDays(-6), DueAt = now.AddDays(-4) },
                Assignments =
                {
                    new TaskAssignment { Assignee = lead, AssignedBy = developer, AssignedAt = now.AddDays(-7) }
                }
            });
    }
}
