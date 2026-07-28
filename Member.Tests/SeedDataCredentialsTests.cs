using Microsoft.Extensions.Configuration;
using MidProject.Data;
using MidProject.Services;

namespace Member.Tests;

public sealed class SeedDataCredentialsTests
{
    [Theory]
    [InlineData("SeedData:AdminPassword")]
    [InlineData("SeedData:UserPassword")]
    public void FromConfiguration_WhenRequiredPasswordIsMissing_FailsClosed(string missingKey)
    {
        var values = new Dictionary<string, string?>
        {
            ["SeedData:AdminPassword"] = Guid.NewGuid().ToString("N"),
            ["SeedData:UserPassword"] = Guid.NewGuid().ToString("N")
        };
        values.Remove(missingKey);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => SeedDataCredentials.FromConfiguration(configuration));

        Assert.Contains(missingKey, exception.Message);
    }

    [Fact]
    public async Task SeedMembersAsync_UsesInjectedPasswords()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedData.SeedUserLevelsAsync(db);
        var adminPassword = Guid.NewGuid().ToString("N");
        var userPassword = Guid.NewGuid().ToString("N");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedData:AdminPassword"] = adminPassword,
                ["SeedData:UserPassword"] = userPassword
            })
            .Build();

        await SeedData.SeedMembersAsync(
            db,
            new DateTime(2026, 7, 29, 10, 0, 0),
            SeedDataCredentials.FromConfiguration(configuration));

        var admin = Assert.Single(db.Members.Where(member => member.Role == "Admin"));
        var users = db.Members.Where(member => member.Role == "User").ToList();
        Assert.NotEmpty(users);
        Assert.True(PasswordHashService.VerifyPassword(adminPassword, admin.PasswordHash));
        Assert.All(
            users,
            user => Assert.True(
                PasswordHashService.VerifyPassword(userPassword, user.PasswordHash)));
    }
}
