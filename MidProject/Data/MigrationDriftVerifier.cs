using Microsoft.EntityFrameworkCore;

namespace MidProject.Data;

public static class MigrationDriftVerifier
{
    public const string CommandArgument = "--verify-migration-drift";

    public static int Run()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=MigrationDriftOnly;User Id=unused;Password=unused;TrustServerCertificate=True")
            .Options;
        using var context = new AppDbContext(options);

        if (context.Database.HasPendingModelChanges())
        {
            Console.Error.WriteLine(
                "Migration drift detected: AppDbContext and the latest Model Snapshot differ.");
            return 1;
        }

        Console.WriteLine("Migration drift check passed.");
        return 0;
    }
}
