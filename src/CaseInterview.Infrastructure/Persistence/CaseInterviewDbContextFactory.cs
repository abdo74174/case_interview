using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaseInterview.Infrastructure.Persistence;

public class CaseInterviewDbContextFactory : IDesignTimeDbContextFactory<CaseInterviewDbContext>
{
    public CaseInterviewDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=caseinterview_db;Username=postgres;Password=postgres";

        var builder = new DbContextOptionsBuilder<CaseInterviewDbContext>();
        builder.UseNpgsql(connectionString, o =>
        {
            o.UseVector();
            o.MigrationsAssembly(typeof(CaseInterviewDbContext).Assembly.FullName);
        });

        return new CaseInterviewDbContext(builder.Options);
    }
}
