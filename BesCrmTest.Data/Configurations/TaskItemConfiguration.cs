using BesCrmTest.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BesCrmTest.Data.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks", t =>
        {
            // DueAt не може бути раніше за PlannedStartAt
            t.HasCheckConstraint(
                "CK_Tasks_DueAt_NotBefore_PlannedStartAt",
                "\"DueAt\" >= \"PlannedStartAt\"");

            // CompletedAt заповнений тоді й тільки тоді, коли статус Completed
            t.HasCheckConstraint(
                "CK_Tasks_CompletedAt_OnlyForCompleted",
                $"(\"StatusId\" = {(int)TaskStatusCode.Completed}) = (\"CompletedAt\" IS NOT NULL)");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.Property(t => t.StatusId)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        // Дедлайн — owned type: його властивості стають колонками таблиці Tasks
        builder.OwnsOne(t => t.Deadline, deadline =>
        {
            deadline.Property(d => d.PlannedStartAt)
                .HasColumnName("PlannedStartAt")
                .IsRequired();

            deadline.Property(d => d.DueAt)
                .HasColumnName("DueAt")
                .IsRequired();

            // Індекс для контролю строків (прострочені задачі, сортування за дедлайном)
            deadline.HasIndex(d => d.DueAt);
        });

        builder.Navigation(t => t.Deadline)
            .IsRequired();

        // Статус: видалити статус, поки є задачі з ним, не можна
        builder.HasOne(t => t.Status)
            .WithMany(s => s.Tasks)
            .HasForeignKey(t => t.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // Автор: видалити співробітника, який має створені задачі, не можна
        builder.HasOne(t => t.CreatedBy)
            .WithMany(e => e.CreatedTasks)
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.StatusId);
    }
}
