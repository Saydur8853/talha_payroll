using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace VisorHR.Data;

public interface IUnitDbContextFactory
{
    UnitDbContext CreateDbContext(string unit);
}

public class UnitDbContextFactory : IUnitDbContextFactory
{
    private readonly IConfiguration _configuration;

    public UnitDbContextFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public UnitDbContext CreateDbContext(string unit)
    {
        var connectionString = BuildMySqlConnectionString();

        var options = new DbContextOptionsBuilder<UnitDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;

        return new UnitDbContext(options);
    }

    private string BuildMySqlConnectionString()
    {
        var database = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "visorDB";
        var user = Environment.GetEnvironmentVariable("MYSQL_USER") ?? "root";
        var password = Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "admin";
        var host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "127.0.0.1";
        var port = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306";

        return $"Server={host};Port={port};Database={database};User={user};Password={password};";
    }
}
