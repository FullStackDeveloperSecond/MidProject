using Microsoft.EntityFrameworkCore;
using MidProject.Repositories;
using MidProject.Services;

namespace Member.Tests;

public sealed class TagServiceTests
{
    [Fact]
    public async Task CreateAsync_NormalizesUnicode_AndAssignsNextSortOrder()
    {
        await using var context = InMemoryDbContextFactory.Create();
        context.Tags.Add(new() { TagName = "既有標籤", SortOrder = 4 });
        await context.SaveChangesAsync();
        var clock = new FakeTaipeiClock();
        var repository = new TagRepository(context, clock);
        var service = new TagService(repository);

        var result = await service.CreateAsync("  ＴＥＳＴ  ", adminId: 1);

        Assert.True(result.Success);
        var saved = await context.Tags.SingleAsync(tag => tag.TagName == "TEST");
        Assert.Equal(5, saved.SortOrder);
    }

    [Fact]
    public async Task ToggleAsync_UsesTaipeiClockForDeletionMetadata()
    {
        await using var context = InMemoryDbContextFactory.Create();
        var tag = new MidProject.Models.Tag { TagName = "測試" };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();
        var clock = new FakeTaipeiClock
        {
            Now = new DateTime(2026, 7, 29, 16, 0, 0, DateTimeKind.Unspecified)
        };
        var repository = new TagRepository(context, clock);
        var service = new TagService(repository);

        var result = await service.ToggleAsync(tag.TagID, adminId: 9);

        Assert.True(result);
        Assert.True(tag.IsDeleted);
        Assert.Equal(clock.Now, tag.DeletedAt);
        Assert.Equal(9, tag.DeletedBy);
    }
}
