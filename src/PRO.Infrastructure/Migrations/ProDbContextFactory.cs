using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PRO.Infrastructure.Persistence;

namespace PRO.Infrastructure.Migrations;

public class ProDbContextFactory : IDesignTimeDbContextFactory<ProDbContext>
{
    public ProDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProDbContext>();
        var connectionString =
            Environment.GetEnvironmentVariable("PRO_ConnectionStrings__PostgreSQL")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL")
            ?? "Host=localhost;Port=5432;Database=pro;Username=postgres;Password=CHANGE_ME";

        optionsBuilder.UseNpgsql(connectionString);

        return new ProDbContext(optionsBuilder.Options);
    }
}
