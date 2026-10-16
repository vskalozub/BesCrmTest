# BesCrmTest — модуль Task Management для внутрішньої CRM

Backend / data layer модуля постановки, виконання та контролю задач співробітників.
**PostgreSQL + EF Core 10**: модель даних, зв'язки, міграції, індекси, обмеження, seed-дані,
CRUD-сценарії та перевірки бізнес-правил.

## Структура

```
BesCrmTest.Data/                бібліотека data layer
  Entities/                     Employee, TaskItem, TaskItemStatus, Deadline, TaskAssignment
  Configurations/               Fluent API (IEntityTypeConfiguration<T>) — без атрибутів
  Migrations/                   InitialCreate, AddBusinessRuleTriggers
  Seed/CrmSeeder.cs             тестові дані (співробітники, задачі, призначення)
  Services/TaskService.cs       CRUD-сценарії + перевірки бізнес-правил
  CrmDbContext.cs
  CrmDbContextFactory.cs        фабрика для dotnet ef
BesCrmTest.Demo/                консольна демонстрація сценаріїв і правил
scripts/pg-local.ps1            керування локальним portable PostgreSQL (необов'язково)
```

## Модель даних

| Таблиця | Призначення |
|---|---|
| `Employees` | Співробітники. `Email` унікальний, `IsActive` — ознака активності |
| `TaskStatuses` | Довідник статусів: New, In progress, Completed, Cancelled. `IsFinal` — фінальний статус |
| `Tasks` | Задачі: статус, автор, `CreatedAt`, `CompletedAt`, дедлайн (`PlannedStartAt`, `DueAt`) |
| `TaskAssignments` | Призначення: задача, виконавець (`AssigneeId`), хто призначив (`AssignedById`), `AssignedAt` |

- **Дедлайн** — owned type `Deadline`, зберігається колонками в `Tasks`, тому правило
  `DueAt >= PlannedStartAt` задано CHECK-обмеженням.
- **Статуси** — таблиця-довідник + enum `TaskStatusCode` (Id у таблиці = значення enum).
- **Видалення:** Cascade лише `Tasks → TaskAssignments`; всі інші зв'язки — Restrict,
  щоб видалення співробітника чи статусу не знищувало історію.
- **Індекси:** унікальні `Employees.Email` і `(TaskId, AssigneeId)`; `AssigneeId` — для вибірки
  задач за виконавцем; `StatusId`, `DueAt` — для фільтрації та контролю строків.

## Бізнес-правила

Кожне правило перевіряється в `TaskService` (зрозуміла помилка `BusinessRuleException`)
і додатково захищене на рівні БД — навіть якщо записувати дані в обхід сервісу.

| # | Правило | `TaskService` | БД |
|---|---|---|---|
| 1 | `CompletedAt` обов'язковий тільки для статусу Completed | `ChangeStatusAsync` | CHECK `CK_Tasks_CompletedAt_OnlyForCompleted` |
| 2 | `DueAt` не може бути раніше `PlannedStartAt` | `CreateTaskAsync` | CHECK `CK_Tasks_DueAt_NotBefore_PlannedStartAt` |
| 3 | Неактивному співробітнику не можна призначити нову задачу | `AssignTaskAsync` | тригер `trg_task_assignments_assignee_active` |
| 4 | Completed і Cancelled — фінальні, з них не можна перейти в інший статус | `ChangeStatusAsync` | тригер `trg_tasks_final_status` |
| 5 | Задача не може бути призначена самому собі (`AssigneeId <> AssignedById`) | `AssignTaskAsync` | CHECK `CK_TaskAssignments_NotSelfAssigned` |

Правила 3 і 4 реалізовано тригерами, бо CHECK-обмеження бачить лише один рядок однієї таблиці:
правилу 3 потрібна інша таблиця (`Employees.IsActive`), правилу 4 — попереднє значення статусу.

## CRUD-сценарії (`TaskService`)

| Метод | Сценарій |
|---|---|
| `CreateTaskAsync` | Створити задачу (завжди в статусі New) |
| `ChangeStatusAsync` | Змінити статус; при переході в Completed заповнює `CompletedAt` |
| `GetTasksByAssigneeAsync` | Список задач за виконавцем (найближчий дедлайн — першим) |
| `AssignTaskAsync` | Призначити задачу виконавцю — тут перевіряються правила 3 і 5 |

## Seed-дані

- **Статуси** — через `HasData`, тобто входять у міграцію: це довідник, потрібний у будь-якому середовищі.
- **Співробітники, задачі, призначення** — через `UseSeeding` / `UseAsyncSeeding` (`CrmSeeder`),
  виконуються після міграцій і лише в порожню БД. `HasData` для них не використано: з явними Id
  PostgreSQL не зсуває identity-послідовність, і перший звичайний INSERT конфліктував би за ключем.

3 співробітники (один неактивний — для перевірки правила 3), 3 задачі в різних статусах, 3 призначення.

## Запуск

Потрібно: .NET 10 SDK, PostgreSQL (перевірено на 18), інструмент `dotnet-ef`:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.4
```

Рядок підключення за замовчуванням —
`Host=localhost;Port=5432;Database=bescrm;Username=postgres;Password=postgres`.
Інший задається змінною середовища:

```powershell
$env:BESCRM_CONNECTION = "Host=localhost;Port=5432;Database=bescrm;Username=...;Password=..."
```

### Створити БД (міграції + seed-дані)

```powershell
dotnet ef database update --project BesCrmTest.Data
```

### Демонстрація

```powershell
dotnet run --project BesCrmTest.Demo
```

Демо створює окрему БД `bescrm_demo` (на тому ж сервері), виконує сценарії й показує,
як кожне правило відхиляється спершу сервісом, а потім — базою даних при записі в обхід сервісу.
В кінці БД видаляється; з ключем `--keep` залишається для перегляду:

```powershell
dotnet run --project BesCrmTest.Demo -- --keep
```

Фрагмент виводу:

```
=== Призначення задачі ===
  OK          Задачу #4 призначено: Олена Коваль (призначив Іван Петренко)
  ВІДХИЛЕНО   Правило 5: призначення самому собі
              A task cannot be assigned to yourself.
  ВІДХИЛЕНО   Правило 3: призначення неактивному (Андрій Шевченко)
              Employee 3 is inactive and cannot be assigned a new task.

=== Захист на рівні БД (запис в обхід TaskService) ===
  ВІДХИЛЕНО   Правило 3 (тригер): призначення неактивному
              [trigger] Employee 3 is inactive and cannot be assigned a new task.
  ВІДХИЛЕНО   Правило 4 (тригер): Completed → New
              [trigger] Task 4 is in a final status and its status cannot be changed.
```

### Корисні команди

```powershell
dotnet ef migrations list --project BesCrmTest.Data                  # застосовані міграції
dotnet ef migrations script --project BesCrmTest.Data                # SQL усіх міграцій (без застосування)
dotnet ef database drop --force --project BesCrmTest.Data            # видалити БД
```

> Імена таблиць і колонок створені EF у PascalCase, тому в SQL їх потрібно брати в лапки:
> `SELECT * FROM "Tasks";`

## Примітки

- Усі дати зберігаються в UTC (`timestamp with time zone`); `TaskService` приводить вхідні дати до UTC.
- `CrmDbContextFactory` використовується лише інструментами `dotnet ef` і демо-проєктом не потрібна.
