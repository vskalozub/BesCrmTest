using BesCrmTest.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BesCrmTest.Data.Configurations;

public class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable("TaskAssignments", t =>
        {
            // Задачу не можна призначити самому собі
            t.HasCheckConstraint(
                "CK_TaskAssignments_NotSelfAssigned",
                "\"AssigneeId\" <> \"AssignedById\"");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AssignedAt)
            .IsRequired();

        // Задача: під час видалення задачі її призначення видаляються разом із нею
        builder.HasOne(a => a.Task)
            .WithMany(t => t.Assignments)
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Виконавець: два зв'язки з Employee, тому явно вказуємо, яка колекція належить до якого зв'язку
        builder.HasOne(a => a.Assignee)
            .WithMany(e => e.Assignments)
            .HasForeignKey(a => a.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Хто призначив
        builder.HasOne(a => a.AssignedBy)
            .WithMany(e => e.GivenAssignments)
            .HasForeignKey(a => a.AssignedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Один співробітник призначається на задачу не більше одного разу
        builder.HasIndex(a => new { a.TaskId, a.AssigneeId })
            .IsUnique();

        // Для запиту "задачі за виконавцем"
        builder.HasIndex(a => a.AssigneeId);
    }
}
