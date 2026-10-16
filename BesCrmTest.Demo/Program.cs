using System.Text;
using BesCrmTest.Data;
using BesCrmTest.Data.Entities;
using BesCrmTest.Data.Seed;
using BesCrmTest.Data.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Демонстрація CRUD-сценаріїв і бізнес-правил.
// Працює з окремою тимчасовою БД bescrm_demo: створює її міграціями (з тригерами та seed-даними)
// і видаляє в кінці. Ключ --keep залишає БД, щоб переглянути результат у pgAdmin.

Console.OutputEncoding = Encoding.UTF8;

var keepDatabase = args.Contains("--keep");

// Сервер і облікові дані — як у CrmDbContextFactory, змінюється тільки назва БД
var connection = new NpgsqlConnectionStringBuilder(
    Environment.GetEnvironmentVariable("BESCRM_CONNECTION")
    ?? "Host=localhost;Port=5432;Database=bescrm;Username=postgres;Password=postgres")
{
    Database = "bescrm_demo"
};

CrmDbContext CreateContext()
{
    var options = new DbContextOptionsBuilder<CrmDbContext>().UseNpgsql(connection.ConnectionString);
    options.UseCrmSeeding();
    return new CrmDbContext(options.Options);
}

await using (var setup = CreateContext())
{
    Console.WriteLine($"Підготовка БД {connection.Database}: міграції та seed-дані...");
    await setup.Database.EnsureDeletedAsync();
    await setup.Database.MigrateAsync();
}

try
{
    await using var db = CreateContext();
    var service = new TaskService(db);

    var lead = await db.Employees.SingleAsync(e => e.Email == "ivan.petrenko@bescrm.local");
    var developer = await db.Employees.SingleAsync(e => e.Email == "olena.koval@bescrm.local");
    var inactive = await db.Employees.SingleAsync(e => !e.IsActive);
    var now = DateTime.UtcNow;

    Section("Створення задачі");
    var task = await service.CreateTaskAsync(
        "Підготувати демо", "Показати CRUD-сценарії та бізнес-правила", lead.Id, now, now.AddDays(2));
    Ok($"Створено задачу #{task.Id} \"{task.Title}\": статус {task.StatusId}, автор {lead.FullName}, " +
       $"строк {Format(task.Deadline.PlannedStartAt)} – {Format(task.Deadline.DueAt)}");

    await Rejected("Правило 2: DueAt раніше за PlannedStartAt",
        () => service.CreateTaskAsync("Некоректний дедлайн", null, lead.Id, now, now.AddDays(-1)));

    Section("Призначення задачі");
    await service.AssignTaskAsync(task.Id, developer.Id, lead.Id);
    Ok($"Задачу #{task.Id} призначено: {developer.FullName} (призначив {lead.FullName})");

    await Rejected("Правило 5: призначення самому собі",
        () => service.AssignTaskAsync(task.Id, lead.Id, lead.Id));
    await Rejected($"Правило 3: призначення неактивному ({inactive.FullName})",
        () => service.AssignTaskAsync(task.Id, inactive.Id, lead.Id));

    Section("Зміна статусу");
    task = await service.ChangeStatusAsync(task.Id, TaskStatusCode.InProgress);
    Ok($"#{task.Id}: → {task.Status.Name}, CompletedAt = {Format(task.CompletedAt)}");
    task = await service.ChangeStatusAsync(task.Id, TaskStatusCode.Completed);
    Ok($"#{task.Id}: → {task.Status.Name}, CompletedAt = {Format(task.CompletedAt)}  (правило 1)");

    await Rejected("Правило 4: вихід із фінального статусу Completed",
        () => service.ChangeStatusAsync(task.Id, TaskStatusCode.InProgress));

    Section($"Задачі виконавця: {developer.FullName}");
    foreach (var t in await service.GetTasksByAssigneeAsync(developer.Id))
        Console.WriteLine($"  #{t.Id,-3} {t.Title,-32} {t.Status.Name,-12} до {Format(t.Deadline.DueAt)}");

    Section("Захист на рівні БД (запис в обхід TaskService)");
    var newTaskId = await db.Tasks.Where(t => t.StatusId == TaskStatusCode.New).Select(t => t.Id).FirstAsync();
    var completedTaskId = task.Id;

    await RejectedByDatabase("Правило 1 (CHECK): CompletedAt у задачі зі статусом New", async ctx =>
    {
        var t = await ctx.Tasks.FindAsync(newTaskId);
        t!.CompletedAt = DateTime.UtcNow;
    });
    await RejectedByDatabase("Правило 2 (CHECK): DueAt раніше за PlannedStartAt", async ctx =>
    {
        var t = await ctx.Tasks.FindAsync(newTaskId);
        t!.Deadline.DueAt = t.Deadline.PlannedStartAt.AddDays(-1);
    });
    await RejectedByDatabase("Правило 3 (тригер): призначення неактивному", ctx =>
    {
        ctx.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = newTaskId, AssigneeId = inactive.Id, AssignedById = lead.Id, AssignedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    });
    await RejectedByDatabase("Правило 4 (тригер): Completed → New", async ctx =>
    {
        var t = await ctx.Tasks.FindAsync(completedTaskId);
        t!.StatusId = TaskStatusCode.New;
        t.CompletedAt = null;
    });
    await RejectedByDatabase("Правило 5 (CHECK): призначення самому собі", ctx =>
    {
        ctx.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = newTaskId, AssigneeId = lead.Id, AssignedById = lead.Id, AssignedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    });
}
finally
{
    if (keepDatabase)
    {
        Console.WriteLine($"\nБД {connection.Database} залишено для перегляду (--keep).");
    }
    else
    {
        await using var cleanup = CreateContext();
        await cleanup.Database.EnsureDeletedAsync();
        Console.WriteLine($"\nБД {connection.Database} видалено.");
    }
}

static void Section(string title) => Console.WriteLine($"\n=== {title} ===");

static void Ok(string message) => Console.WriteLine($"  OK          {message}");

static string Format(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm") ?? "—";

// Очікуємо, що TaskService відхилить операцію
static async Task Rejected(string rule, Func<Task> action)
{
    try
    {
        await action();
        Console.WriteLine($"  ПОМИЛКА     {rule}: операцію не відхилено!");
    }
    catch (Exception ex) when (ex is BusinessRuleException or EntityNotFoundException)
    {
        Console.WriteLine($"  ВІДХИЛЕНО   {rule}\n              {ex.Message}");
    }
}

// Очікуємо, що PostgreSQL відхилить зміну (CHECK-обмеження або тригер).
// Кожна спроба — в окремому контексті, щоб відхилена зміна не лишилась у ChangeTracker.
async Task RejectedByDatabase(string rule, Func<CrmDbContext, Task> change)
{
    await using var ctx = CreateContext();
    await change(ctx);
    try
    {
        await ctx.SaveChangesAsync();
        Console.WriteLine($"  ПОМИЛКА     {rule}: БД прийняла зміну!");
    }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
    {
        var source = pg.ConstraintName ?? "trigger";
        Console.WriteLine($"  ВІДХИЛЕНО   {rule}\n              [{source}] {pg.MessageText}");
    }
}
