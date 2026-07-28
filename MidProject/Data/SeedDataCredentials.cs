using Microsoft.Extensions.Configuration;

namespace MidProject.Data;

public sealed record SeedDataCredentials
{
    private SeedDataCredentials(string adminPassword, string userPassword)
    {
        AdminPassword = adminPassword;
        UserPassword = userPassword;
    }

    public string AdminPassword { get; }
    public string UserPassword { get; }

    public static SeedDataCredentials FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new SeedDataCredentials(
            Require(configuration, "SeedData:AdminPassword"),
            Require(configuration, "SeedData:UserPassword"));
    }

    private static string Require(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} is required when Development SeedData is enabled. "
                + "Set it with .NET User Secrets or an environment variable.");
        }

        return value;
    }
}
