using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Infrastructure.Data;

public class ErpDbContextFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        // 1️⃣ Read environment name, e.g. "Cloud" or "Local"
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        // 2️⃣ Load appsettings + environment-specific file
        var cfg = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .Build();

        // 3️⃣ Read the connection string called "Db"
        var cs = cfg.GetConnectionString("Db");

        // 4️⃣ Build the context options and return it
        var opts = new DbContextOptionsBuilder<ErpDbContext>()
            .UseSqlServer(cs)
            .Options;

        return new ErpDbContext(opts);
    }
}
