using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TraceCore.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TraceCoreDbContext>
{
    public TraceCoreDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TraceCoreDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=TraceCoreDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True",
                b => b.MigrationsAssembly(typeof(TraceCoreDbContext).Assembly.FullName))
            .Options;

        return new TraceCoreDbContext(options);
    }
}
