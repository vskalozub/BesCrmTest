using BesCrmTest.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BesCrmTest.Data;

// Фабрика для інструментів EF (dotnet ef migrations / database update):
// створює контекст без застосунку-хоста.
public class CrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=bescrm;Username=postgres;Password=postgres";

    public CrmDbContext CreateDbContext(string[] args)
    {
        // Рядок підключення: зі змінної середовища BESCRM_CONNECTION або локальний за замовчуванням
        var connectionString = Environment.GetEnvironmentVariable("BESCRM_CONNECTION")
            ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<CrmDbContext>();
        options.UseNpgsql(connectionString);
        options.UseCrmSeeding();

        return new CrmDbContext(options.Options);
    }
}
