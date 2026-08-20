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
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=coffee.db")
            .Options;

        return new AppDbContext(options);
    }
}
