using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Repositories;

namespace Member.Tests;

public sealed class RestaurantRepositoryLifecycleTests
{
    [Fact]
    public async Task RestoreAsync_ClearsDeletionMetadata_AndUsesTaipeiClock()
    {
        await using var context = InMemoryDbContextFactory.Create();
        var clock = new FakeTaipeiClock
        {
            Now = new DateTime(2026, 7, 29, 15, 30, 0, DateTimeKind.Unspecified)
        };
        var restaurant = new Restaurant
        {
            Name = "測試餐廳",
            City = "台北市",
            District = "大安區",
            DetailedAddress = "測試路 1 號",
            MemberID = 1,
            IsDeleted = true,
            DeletedAt = new DateTime(2026, 7, 20),
            DeletedBy = 7,
            DeleteReason = "測試停用"
        };
        context.Restaurants.Add(restaurant);
        await context.SaveChangesAsync();
        var repository = new RestaurantRepository(context, clock);

        await repository.RestoreAsync(restaurant.RestaurantID);

        var restored = await context.Restaurants.SingleAsync();
        Assert.False(restored.IsDeleted);
        Assert.Null(restored.DeletedAt);
        Assert.Null(restored.DeletedBy);
        Assert.Null(restored.DeleteReason);
        Assert.Equal(clock.Now, restored.UpdatedAt);
    }
}
