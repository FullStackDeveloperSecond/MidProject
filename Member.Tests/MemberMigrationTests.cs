using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using MidProject.Data;
using Xunit;

namespace Member.Tests;

public sealed class MemberMigrationTests
{
    [Fact]
    public void AddMemberRowVersionMigration_IsDiscoverableAndModelUsesConcurrencyToken()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=MigrationDiscoveryOnly;User Id=unused;Password=unused;TrustServerCertificate=True")
            .Options;
        using var context = new AppDbContext(options);

        var migrations = context.GetService<IMigrationsAssembly>().Migrations;
        Assert.Contains("20260728090000_AddMemberRowVersion", migrations.Keys);

        var rowVersion = context.Model
            .FindEntityType("MidProject.Models.Member")!
            .FindProperty("RowVersion");
        Assert.NotNull(rowVersion);
        Assert.True(rowVersion!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
    }
}
