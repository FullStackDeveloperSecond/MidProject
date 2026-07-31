using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MidProject.Data;
using MidProject.Models;
using MidProject.Repositories;
using MidProject.Repositories.IRepositories;

namespace Member.Tests;

public sealed class MemberRepositoryStatusSortingTests
{
    [Theory]
    [InlineData("status_asc", "Normal,Warning,Muted,Suspended")]
    [InlineData("status_desc", "Suspended,Muted,Warning,Normal")]
    public async Task GetIndexPageDataAsync_SortsStatusesByBusinessOrder(
        string sortBy,
        string expectedCsv)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var context = new AppDbContext(options);
        var level = new UserLevel
        {
            LevelID = 1,
            LevelName = "測試等級",
            MinExp = 0
        };
        context.UserLevels.Add(level);

        var statuses = new[] { "Muted", "Normal", "Suspended", "Warning" };
        for (var index = 0; index < statuses.Length; index++)
        {
            context.Members.Add(new MidProject.Models.Member
            {
                MemberID = index + 1,
                UserName = $"member{index}",
                Email = $"member{index}@example.com",
                PasswordHash = "hash",
                Role = "User",
                Status = statuses[index],
                IsActive = true,
                LevelID = level.LevelID,
                CreatedAt = new DateTime(2026, 7, 30).AddMinutes(index),
                UpdatedAt = new DateTime(2026, 7, 30)
            });
        }
        await context.SaveChangesAsync();

        var repository = new MemberRepository(context);
        var page = await repository.GetIndexPageDataAsync(new MemberListFilter(
            Keyword: null,
            StatusFilter: null,
            LevelFilter: null,
            SortBy: sortBy,
            ShowAbnormal: false,
            TodayOnly: false,
            Page: 1,
            TodayDate: new DateTime(2026, 7, 30),
            PageSize: 10));

        Assert.Equal(
            expectedCsv.Split(','),
            page.Members.Select(member => member.Status));
    }
}
