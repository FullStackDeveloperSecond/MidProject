using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MidProject.Data;

/// <summary>
/// Creates AppDbContext for EF Core CLI commands without starting the web app or running SeedData.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var projectDirectory = FindProjectDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(projectDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<AppDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. " +
                "Use .NET User Secrets or the ConnectionStrings__DefaultConnection environment variable.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }

    private static string FindProjectDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        if (File.Exists(Path.Combine(currentDirectory, "appsettings.json")))
        {
            return currentDirectory;
        }

        var nestedProjectDirectory = Path.Combine(currentDirectory, "MidProject");
        if (File.Exists(Path.Combine(nestedProjectDirectory, "appsettings.json")))
        {
            return nestedProjectDirectory;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the MidProject directory containing appsettings.json.");
    }
}
