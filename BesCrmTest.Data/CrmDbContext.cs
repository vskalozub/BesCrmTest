using BesCrmTest.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BesCrmTest.Data;

public class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskItemStatus> TaskStatuses => Set<TaskItemStatus>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Підключає всі класи IEntityTypeConfiguration<T> з цієї збірки (папка Configurations)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }
}
