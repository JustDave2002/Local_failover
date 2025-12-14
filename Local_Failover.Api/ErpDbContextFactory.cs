using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Infrastructure.Data;

public class ErpDbContextFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        // Determine environment: Cloud / Local / Development
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        // Always load config from project directory (not working directory)
        var basePath = Directory.GetCurrentDirectory();

        // Load appsettings + environment-specific file
        var cfg = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{env}.json", optional: false)
            .Build();

        var cs = cfg.GetConnectionString("Db");

        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new Exception($"Connection string 'Db' not found for ENV={env}. BasePath={basePath}");
        }

        // Build the context options and return it
        var builder = new DbContextOptionsBuilder<ErpDbContext>();

        if (env == "Cloud")
        {
            builder.UseSqlite(cs);
        } 
        else
        {
            builder.UseSqlServer(cs);
        }

        return new ErpDbContext(builder.Options);
    }
}
