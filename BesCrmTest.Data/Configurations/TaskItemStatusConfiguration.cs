using BesCrmTest.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BesCrmTest.Data.Configurations;

public class TaskItemStatusConfiguration : IEntityTypeConfiguration<TaskItemStatus>
{
    public void Configure(EntityTypeBuilder<TaskItemStatus> builder)
    {
        builder.ToTable("TaskStatuses");

        builder.HasKey(s => s.Id);

        // Id — значення enum, зберігається як int і не генерується базою
        builder.Property(s => s.Id)
            .HasConversion<int>()
            .ValueGeneratedNever();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.IsFinal)
            .IsRequired();

        // Довідник статусів потрапляє в міграцію (Id фіксовані й збігаються з enum)
        builder.HasData(
            new TaskItemStatus { Id = TaskStatusCode.New, Name = "New", IsFinal = false },
            new TaskItemStatus { Id = TaskStatusCode.InProgress, Name = "In progress", IsFinal = false },
            new TaskItemStatus { Id = TaskStatusCode.Completed, Name = "Completed", IsFinal = true },
            new TaskItemStatus { Id = TaskStatusCode.Cancelled, Name = "Cancelled", IsFinal = true });
    }
}
