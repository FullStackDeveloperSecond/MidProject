using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MidProject.Data;
using MidProject.Models;
using MidProject.Repositories;
using MidProject.Services;
using Xunit;

namespace Reviews.Tests;

/// <summary>
/// 覆蓋 Terry_缺失清單.md T-R-09 要求的評論模組行為：列表篩選、詳細、軟刪除、還原、
/// 圖片刪除、以及軟刪除／還原後的餐廳統計重算。用 EF Core InMemory provider 頂替真正的
/// SQL Server，每個測試案例都用全新的資料庫名稱，彼此不會互相汙染。
/// </summary>
public class ReviewServiceTests
{
    private static AppDbContext CreateContext()
    {
        // InMemory provider 沒有真的交易可用；ConfigureWarnings 讓 ReviewService 裡的
        // BeginTransactionAsync 變成無害的 no-op，而不是被 EF Core 升級成例外丟出來。
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static (AppDbContext Db, ReviewService Service, FakeTaipeiClock Clock) CreateSut()
    {
        var db = CreateContext();
        var clock = new FakeTaipeiClock();
        var repo = new ReviewRepository(db, clock);
        var service = new ReviewService(repo, db, clock);
        return (db, service, clock);
    }

    private static Restaurant SeedRestaurant(AppDbContext db, int reviewCount = 0, decimal averageRating = 0m)
    {
        var restaurant = new Restaurant
        {
            Name = "測試餐廳",
            City = "台北市",
            District = "大安區",
            DetailedAddress = "測試路 1 號",
            MemberID = 1,
            ReviewCount = reviewCount,
            AverageRating = averageRating,
        };
        db.Restaurants.Add(restaurant);
        db.SaveChanges();
        return restaurant;
    }

    private static Member SeedMember(AppDbContext db, string userName = "tester")
    {
        var member = new Member
        {
            UserName = userName,
            Email = $"{userName}@example.com",
            PasswordHash = "hash",
            LevelID = 1,
        };
        db.Members.Add(member);
        db.SaveChanges();
        return member;
    }

    private static Review SeedReview(AppDbContext db, int restaurantId, int memberId, int rating = 5, bool isDeleted = false, string status = "Active")
    {
        var review = new Review
        {
            RestaurantID = restaurantId,
            MemberID = memberId,
            Rating = rating,
            Content = "測試評論內容",
            Status = status,
            IsDeleted = isDeleted,
        };
        db.Reviews.Add(review);
        db.SaveChanges();
        return review;
    }

    [Fact]
    public async Task GetReviewListAsync_FiltersByRestaurantId()
    {
        var (db, service, _) = CreateSut();
        var member = SeedMember(db);
        var restaurantA = SeedRestaurant(db);
        var restaurantB = SeedRestaurant(db);
        SeedReview(db, restaurantA.RestaurantID, member.MemberID);
        SeedReview(db, restaurantA.RestaurantID, member.MemberID);
        SeedReview(db, restaurantB.RestaurantID, member.MemberID);

        var result = await service.GetReviewListAsync("all", null, null, "all", "time", "desc", 1, restaurantA.RestaurantID);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, r => Assert.Equal(restaurantA.RestaurantID, r.RestaurantID));
    }

    [Fact]
    public async Task GetReviewDetailAsync_WhenReviewMissing_ReturnsNull()
    {
        var (_, service, _) = CreateSut();

        var result = await service.GetReviewDetailAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenReviewMissing_ReturnsFalse()
    {
        var (_, service, _) = CreateSut();

        var success = await service.SoftDeleteAsync(999, adminMemberId: 1);

        Assert.False(success);
    }

    [Fact]
    public async Task SoftDeleteAsync_MarksReviewDeleted_AndRecalculatesRestaurantStats()
    {
        var (db, service, clock) = CreateSut();
        var member = SeedMember(db);
        // 餐廳原本兩則評論（5 星、3 星），刪掉其中一則之後平均分數跟筆數都要重算
        var restaurant = SeedRestaurant(db, reviewCount: 2, averageRating: 4m);
        var keep = SeedReview(db, restaurant.RestaurantID, member.MemberID, rating: 3);
        var toDelete = SeedReview(db, restaurant.RestaurantID, member.MemberID, rating: 5);

        var success = await service.SoftDeleteAsync(toDelete.ReviewID, adminMemberId: 42);

        Assert.True(success);

        var updatedReview = await db.Reviews.FindAsync(toDelete.ReviewID);
        Assert.NotNull(updatedReview);
        Assert.True(updatedReview!.IsDeleted);
        Assert.Equal(42, updatedReview.DeletedBy);
        Assert.Equal(clock.Now, updatedReview.DeletedAt);

        var updatedRestaurant = await db.Restaurants.FindAsync(restaurant.RestaurantID);
        Assert.NotNull(updatedRestaurant);
        Assert.Equal(1, updatedRestaurant!.ReviewCount);
        Assert.Equal(3m, updatedRestaurant.AverageRating);

        // 沒被刪除的那則評論要維持原樣
        var untouched = await db.Reviews.FindAsync(keep.ReviewID);
        Assert.False(untouched!.IsDeleted);
    }

    [Fact]
    public async Task RestoreAsync_WhenReviewMissing_ReturnsFalse()
    {
        var (_, service, _) = CreateSut();

        var success = await service.RestoreAsync(999);

        Assert.False(success);
    }

    [Fact]
    public async Task RestoreAsync_ClearsDeleteFields_AndRecalculatesRestaurantStats()
    {
        var (db, service, _) = CreateSut();
        var member = SeedMember(db);
        var restaurant = SeedRestaurant(db, reviewCount: 0, averageRating: 0m);
        var review = SeedReview(db, restaurant.RestaurantID, member.MemberID, rating: 4, isDeleted: true);
        review.DeletedAt = new DateTime(2025, 1, 1);
        review.DeletedBy = 7;
        await db.SaveChangesAsync();

        var success = await service.RestoreAsync(review.ReviewID);

        Assert.True(success);

        var restored = await db.Reviews.FindAsync(review.ReviewID);
        Assert.NotNull(restored);
        Assert.False(restored!.IsDeleted);
        Assert.Null(restored.DeletedAt);
        Assert.Null(restored.DeletedBy);

        // 還原後這則評論要被算回餐廳的統計裡
        var updatedRestaurant = await db.Restaurants.FindAsync(restaurant.RestaurantID);
        Assert.Equal(1, updatedRestaurant!.ReviewCount);
        Assert.Equal(4m, updatedRestaurant.AverageRating);
    }

    [Fact]
    public async Task DeleteImageAsync_WhenImageMissing_ReturnsFalse()
    {
        var (_, service, _) = CreateSut();

        var success = await service.DeleteImageAsync(999, reviewId: 999, adminMemberId: 1);

        Assert.False(success);
    }

    [Fact]
    public async Task DeleteImageAsync_SoftDeletesImage_ButKeepsFileUrl()
    {
        var (db, service, clock) = CreateSut();
        var member = SeedMember(db);
        var restaurant = SeedRestaurant(db);
        var review = SeedReview(db, restaurant.RestaurantID, member.MemberID);
        var image = new Image
        {
            UploadedByMemberID = member.MemberID,
            ImageURL = "/uploads/ReviewImage/test.jpg",
            ImageType = "ReviewImage",
        };
        db.Images.Add(image);
        await db.SaveChangesAsync();
        db.ReviewImages.Add(new ReviewImage
        {
            ReviewID = review.ReviewID,
            ImageID = image.ImageID,
        });
        await db.SaveChangesAsync();

        var success = await service.DeleteImageAsync(image.ImageID, review.ReviewID, adminMemberId: 5);

        Assert.True(success);

        var updated = await db.Images.FindAsync(image.ImageID);
        Assert.NotNull(updated);
        Assert.True(updated!.IsDeleted);
        Assert.Equal(5, updated.DeletedBy);
        Assert.Equal(clock.Now, updated.DeletedAt);
        // 規格書 5.5/8：只軟刪除，實體檔案網址要保留，不能被清掉
        Assert.Equal("/uploads/ReviewImage/test.jpg", updated.ImageURL);
    }

    [Fact]
    public async Task DeleteImageAsync_WhenImageBelongsToAnotherReview_DoesNotDelete()
    {
        var (db, service, _) = CreateSut();
        var member = SeedMember(db);
        var restaurant = SeedRestaurant(db);
        var ownerReview = SeedReview(db, restaurant.RestaurantID, member.MemberID);
        var requestedReview = SeedReview(db, restaurant.RestaurantID, member.MemberID);
        var image = new Image
        {
            UploadedByMemberID = member.MemberID,
            ImageURL = "/uploads/ReviewImage/other-review.jpg",
            ImageType = "ReviewImage",
        };
        db.Images.Add(image);
        await db.SaveChangesAsync();
        db.ReviewImages.Add(new ReviewImage
        {
            ReviewID = ownerReview.ReviewID,
            ImageID = image.ImageID,
        });
        await db.SaveChangesAsync();

        var success = await service.DeleteImageAsync(
            image.ImageID,
            requestedReview.ReviewID,
            adminMemberId: 5);

        Assert.False(success);
        Assert.False((await db.Images.FindAsync(image.ImageID))!.IsDeleted);
    }
}
