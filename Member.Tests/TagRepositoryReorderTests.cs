using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Repositories;

namespace Member.Tests;

public class TagRepositoryReorderTests
{
    [Fact]
    public async Task ReorderAsync_CompleteActiveOrder_UpdatesOnlyActiveTags()
    {
        await using var context = InMemoryDbContextFactory.Create();
        var first = new Tag { TagName = "第一個", SortOrder = 0 };
        var second = new Tag { TagName = "第二個", SortOrder = 1 };
        var inactive = new Tag { TagName = "停用", SortOrder = 9, IsDeleted = true };
        context.Tags.AddRange(first, second, inactive);
        await context.SaveChangesAsync();

        var repository = new TagRepository(context);
        var result = await repository.ReorderAsync([second.TagID, first.TagID]);

        Assert.True(result);
        Assert.Equal(1, first.SortOrder);
        Assert.Equal(0, second.SortOrder);
        Assert.Equal(9, inactive.SortOrder);
    }

    [Fact]
    public async Task ReorderAsync_IncompleteDuplicateOrInactiveOrder_IsRejectedWithoutChanges()
    {
        await using var context = InMemoryDbContextFactory.Create();
        var first = new Tag { TagName = "第一個", SortOrder = 3 };
        var second = new Tag { TagName = "第二個", SortOrder = 7 };
        var inactive = new Tag { TagName = "停用", SortOrder = 11, IsDeleted = true };
        context.Tags.AddRange(first, second, inactive);
        await context.SaveChangesAsync();

        var repository = new TagRepository(context);

        Assert.False(await repository.ReorderAsync([first.TagID]));
        Assert.False(await repository.ReorderAsync([first.TagID, first.TagID]));
        Assert.False(await repository.ReorderAsync([first.TagID, inactive.TagID]));

        await context.Entry(first).ReloadAsync();
        await context.Entry(second).ReloadAsync();
        await context.Entry(inactive).ReloadAsync();
        Assert.Equal(3, first.SortOrder);
        Assert.Equal(7, second.SortOrder);
        Assert.Equal(11, inactive.SortOrder);
    }
}
