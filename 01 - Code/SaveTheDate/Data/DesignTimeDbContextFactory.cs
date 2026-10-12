using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SaveTheDate.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var root = File.Exists("SaveTheDate.csproj") ? Directory.GetCurrentDirectory() : Path.Combine(Directory.GetCurrentDirectory(), "01 - Code", "SaveTheDate");
        Directory.CreateDirectory(Path.Combine(root, "App_Data"));
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? $"Data Source={Path.Combine(root, "App_Data", "savethedate.db")}";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
    }
}
