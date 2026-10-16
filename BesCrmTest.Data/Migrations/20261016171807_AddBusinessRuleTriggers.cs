using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BesCrmTest.Data.Migrations
{
    /// <summary>
    /// Тригери для бізнес-правил, які не можна виразити CHECK-обмеженням:
    /// правило 3 (неактивному співробітнику не можна призначити задачу) читає іншу таблицю,
    /// правило 4 (з фінального статусу не можна вийти) порівнює старе й нове значення.
    /// </summary>
    public partial class AddBusinessRuleTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Правило 3: неактивному співробітнику не можна призначити нову задачу
            migrationBuilder.Sql("""
                CREATE FUNCTION fn_task_assignments_assignee_active() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    v_is_active boolean;
                BEGIN
                    SELECT "IsActive" INTO v_is_active FROM "Employees" WHERE "Id" = NEW."AssigneeId";

                    -- Співробітника не знайдено (NULL) — це обробить зовнішній ключ
                    IF v_is_active = false THEN
                        RAISE EXCEPTION 'Employee % is inactive and cannot be assigned a new task.', NEW."AssigneeId"
                            USING ERRCODE = 'check_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_task_assignments_assignee_active
                    BEFORE INSERT OR UPDATE OF "AssigneeId" ON "TaskAssignments"
                    FOR EACH ROW EXECUTE FUNCTION fn_task_assignments_assignee_active();
                """);

            // Правило 4: з фінального статусу (Completed, Cancelled) не можна перейти в інший
            migrationBuilder.Sql("""
                CREATE FUNCTION fn_tasks_final_status() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    v_is_final boolean;
                BEGIN
                    IF NEW."StatusId" IS DISTINCT FROM OLD."StatusId" THEN
                        SELECT "IsFinal" INTO v_is_final FROM "TaskStatuses" WHERE "Id" = OLD."StatusId";

                        IF v_is_final THEN
                            RAISE EXCEPTION 'Task % is in a final status and its status cannot be changed.', OLD."Id"
                                USING ERRCODE = 'check_violation';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_tasks_final_status
                    BEFORE UPDATE OF "StatusId" ON "Tasks"
                    FOR EACH ROW EXECUTE FUNCTION fn_tasks_final_status();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Спочатку тригер, потім функція: тригер залежить від функції
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS trg_tasks_final_status ON "Tasks";""");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_tasks_final_status();");

            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS trg_task_assignments_assignee_active ON "TaskAssignments";""");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_task_assignments_assignee_active();");
        }
    }
}
