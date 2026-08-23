using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoffeeApi.Infrastructure;

/// <summary>
/// Builds an <see cref="AppDbContext"/> for the <c>dotnet ef</c> tooling.
/// Without it the tooling tries to start the web host, which never returns
/// because <c>Program.Main</c> ends in <c>app.Run()</c>. The connection string
/// here is the design-time default: <c>migrations add</c> never opens it, while
/// <c>database update</c> does — override it with <c>--connection</c> to point
/// a command at a different file.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = GetConnectionString(args) ?? "Data Source=coffee.db";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new AppDbContext(options);
    }

    private static string? GetConnectionString(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--connection")
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
