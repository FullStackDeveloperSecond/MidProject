using MidProject.Models.DTOs;
using Xunit;

namespace Reports.Tests;

// 涵蓋：檢舉列表篩選——狀態、精確 RestaurantID / ReviewID（不依賴模糊 Keyword）、目標類型、關鍵字
public sealed class ReportFilteringTests : ReportTestBase
{
    [Fact]
    public async Task Filter_ByStatus_ReturnsOnlyMatching()
    {
        AddReport(status: "Pending", reviewId: ReviewId);
        AddReport(status: "Approved", reviewId: ReviewId, handledAt: Clock.GetNow());
        AddReport(status: "Approved", reviewId: ReviewId, handledAt: Clock.GetNow());

        var result = await Service.GetReportsAsync(new ReportQueryParams { Status = "Approved", PageSize = 50 });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, r => Assert.Equal("Approved", r.Status));
    }

    [Fact]
    public async Task Filter_ByExactRestaurantId_IgnoresOtherTargets()
    {
        AddReport(restaurantId: RestaurantId);
        AddReport(reviewId: ReviewId);
        AddReport(imageId: ImageId);

        var result = await Service.GetReportsAsync(new ReportQueryParams { RestaurantID = RestaurantId, PageSize = 50 });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(RestaurantId, result.Items[0].RestaurantID);
    }

    [Fact]
    public async Task Filter_ByExactReviewId_IgnoresOtherTargets()
    {
        AddReport(restaurantId: RestaurantId);
        AddReport(reviewId: ReviewId);

        var result = await Service.GetReportsAsync(new ReportQueryParams { ReviewID = ReviewId, PageSize = 50 });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(ReviewId, result.Items[0].ReviewID);
    }

    [Fact]
    public async Task Filter_ByTargetType_Review()
    {
        AddReport(restaurantId: RestaurantId);
        AddReport(reviewId: ReviewId);
        AddReport(reviewId: ReviewId);

        var result = await Service.GetReportsAsync(new ReportQueryParams { TargetType = "Review", PageSize = 50 });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, r => Assert.NotNull(r.ReviewID));
    }

    [Fact]
    public async Task Filter_ByKeyword_MatchesReason()
    {
        AddReport(reviewId: ReviewId, reason: "內容含有廣告連結");
        AddReport(reviewId: ReviewId, reason: "與主題無關");

        var result = await Service.GetReportsAsync(new ReportQueryParams { Keyword = "廣告", PageSize = 50 });

        Assert.Equal(1, result.TotalCount);
        Assert.Contains("廣告", result.Items[0].Reason);
    }
}
