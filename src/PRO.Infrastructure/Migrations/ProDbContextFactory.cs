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
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "未配置设计时数据库连接字符串，请设置 PRO_ConnectionStrings__PostgreSQL 或 ConnectionStrings__PostgreSQL。");
        }

        optionsBuilder.UseNpgsql(connectionString);

        return new ProDbContext(optionsBuilder.Options);
    }
}
