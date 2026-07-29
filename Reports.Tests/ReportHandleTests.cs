using MidProject.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Reports.Tests;

// 涵蓋：Handle 驗證（只接受 Approved/Rejected、管理員備註必填與長度）、重複處理、並行控制、被檢舉會員回填（排除 Admin）
public sealed class ReportHandleTests : ReportTestBase
{
    private ReportHandleDto Dto(string status = "Approved", string? note = "已確認違規", string? category = "人身攻擊")
        => new() { Status = status, AdminNote = note, Category = category };

    [Fact]
    public async Task Handle_ValidApproved_SetsStatusHandledAtAndReportedMember()
    {
        var id = AddReport(reviewId: ReviewId);

        var outcome = await Service.HandleReportAsync(id, Dto(), AdminId);

        Assert.Equal(ReportHandleOutcome.Handled, outcome);
        var updated = await Service.GetByIdAsync(id);
        Assert.Equal("Approved", updated!.Status);
        Assert.Equal(OwnerId, updated.ReportedMemberID);        // 被檢舉會員＝評論擁有者
        Assert.Equal(Clock.GetNow(), updated.HandledAt);         // HandledAt 用共用時間服務
        Assert.Equal("admin", updated.HandledByUserName);        // 由目前登入管理員處理

        Db.ChangeTracker.Clear();
        var deletedReview = await Db.Reviews.FindAsync(ReviewId);
        var punishedOwner = await Db.Members.FindAsync(OwnerId);
        var recalculatedRestaurant = await Db.Restaurants.FindAsync(RestaurantId);
        Assert.True(deletedReview!.IsDeleted);
        Assert.Equal(AdminId, deletedReview.DeletedBy);
        Assert.Equal("Warning", punishedOwner!.Status);
        Assert.Equal(1, punishedOwner.WarningCount);
        Assert.Equal(0, recalculatedRestaurant!.ReviewCount);
        Assert.Equal(0m, recalculatedRestaurant.AverageRating);
    }

    [Fact]
    public async Task Handle_ApprovedRestaurant_SoftDeletesRestaurantWithReportReason()
    {
        var id = AddReport(restaurantId: RestaurantId);

        var outcome = await Service.HandleReportAsync(id, Dto(category: "不實資訊"), AdminId);

        Assert.Equal(ReportHandleOutcome.Handled, outcome);
        Db.ChangeTracker.Clear();
        var restaurant = await Db.Restaurants.FindAsync(RestaurantId);
        Assert.True(restaurant!.IsDeleted);
        Assert.Equal(AdminId, restaurant.DeletedBy);
        Assert.Equal(Clock.GetNow(), restaurant.DeletedAt);
        Assert.NotNull(restaurant.DeleteReason);
        Assert.Contains($"案件 #{id}", restaurant.DeleteReason!);
        Assert.Contains("不實資訊", restaurant.DeleteReason!);
    }

    [Fact]
    public async Task Handle_ApprovedImage_SoftDeletesImage()
    {
        var id = AddReport(imageId: ImageId);

        var outcome = await Service.HandleReportAsync(id, Dto(category: "色情內容"), AdminId);

        Assert.Equal(ReportHandleOutcome.Handled, outcome);
        Db.ChangeTracker.Clear();
        var image = await Db.Images.FindAsync(ImageId);
        Assert.True(image!.IsDeleted);
        Assert.Equal(AdminId, image.DeletedBy);
        Assert.Equal(Clock.GetNow(), image.DeletedAt);
    }

    [Fact]
    public async Task Handle_Rejected_DoesNotDeleteContentOrPunishOwner()
    {
        var id = AddReport(reviewId: ReviewId);

        var outcome = await Service.HandleReportAsync(id, Dto(status: "Rejected"), AdminId);

        Assert.Equal(ReportHandleOutcome.Handled, outcome);
        Db.ChangeTracker.Clear();
        var review = await Db.Reviews.FindAsync(ReviewId);
        var owner = await Db.Members.FindAsync(OwnerId);
        Assert.False(review!.IsDeleted);
        Assert.Equal("Normal", owner!.Status);
        Assert.Equal(0, owner.WarningCount);
    }

    [Fact]
    public async Task Handle_FourthApprovedReport_ImmediatelySuspendsOwner()
    {
        for (var i = 0; i < 3; i++)
        {
            AddReport(
                status: "Approved",
                reportedMemberId: OwnerId,
                handledAt: Clock.GetNow().AddDays(-(i + 1)));
        }
        var id = AddReport(reviewId: ReviewId);

        var outcome = await Service.HandleReportAsync(id, Dto(), AdminId);

        Assert.Equal(ReportHandleOutcome.Handled, outcome);
        Db.ChangeTracker.Clear();
        var owner = await Db.Members.FindAsync(OwnerId);
        Assert.Equal("Suspended", owner!.Status);
        Assert.Equal(4, owner.WarningCount);
        Assert.True(owner.IsDeleted);
        Assert.Equal(AdminId, owner.DeletedBy);
        Assert.Equal(Clock.GetNow(), owner.DeletedAt);
    }

    [Fact]
    public async Task Handle_PendingStatus_IsRejected()
    {
        var id = AddReport(reviewId: ReviewId);
        var outcome = await Service.HandleReportAsync(id, Dto(status: "Pending"), AdminId);
        Assert.Equal(ReportHandleOutcome.InvalidStatus, outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceAdminNote_IsRequired(string? note)
    {
        var id = AddReport(reviewId: ReviewId);
        var outcome = await Service.HandleReportAsync(id, Dto(note: note), AdminId);
        Assert.Equal(ReportHandleOutcome.AdminNoteRequired, outcome);
    }

    [Fact]
    public async Task Handle_AdminNoteOver30Chars_IsTooLong()
    {
        var id = AddReport(reviewId: ReviewId);
        var note = new string('備', 31);
        var outcome = await Service.HandleReportAsync(id, Dto(note: note), AdminId);
        Assert.Equal(ReportHandleOutcome.AdminNoteTooLong, outcome);
    }

    [Fact]
    public async Task Handle_InvalidCategory_IsRejected()
    {
        var id = AddReport(reviewId: ReviewId);

        var outcome = await Service.HandleReportAsync(id, Dto(category: "任意分類"), AdminId);

        Assert.Equal(ReportHandleOutcome.InvalidCategory, outcome);
        Assert.Equal("Pending", (await Service.GetByIdAsync(id))!.Status);
    }

    [Fact]
    public async Task Handle_SelfReport_IsRejected()
    {
        var id = AddReport(reviewId: ReviewId, reporterId: OwnerId);

        var outcome = await Service.HandleReportAsync(id, Dto(), AdminId);

        Assert.Equal(ReportHandleOutcome.SelfReportNotAllowed, outcome);
        Assert.Equal("Pending", (await Service.GetByIdAsync(id))!.Status);
    }

    [Fact]
    public async Task Handle_Twice_SecondIsAlreadyHandled_AndResultNotOverridden()
    {
        var id = AddReport(reviewId: ReviewId);

        var first = await Service.HandleReportAsync(id, Dto(status: "Approved"), AdminId);
        var second = await Service.HandleReportAsync(id, Dto(status: "Rejected"), AdminId);

        Assert.Equal(ReportHandleOutcome.Handled, first);
        Assert.Equal(ReportHandleOutcome.AlreadyHandled, second);   // 第二次無法覆蓋
        var updated = await Service.GetByIdAsync(id);
        Assert.Equal("Approved", updated!.Status);                  // 維持第一次的結果
    }

    [Fact]
    public async Task TryHandle_ConcurrentAtomicUpdate_OnlyOneSucceeds()
    {
        var id = AddReport(reviewId: ReviewId);

        // 模擬兩位管理員都通過「讀取為 Pending」後同時嘗試定案：條件式原子更新只讓一個成功
        var first = await Repository.TryHandleAsync(id, "Approved", "人身攻擊", "備註A", OwnerId, AdminId, Clock.GetNow());
        var second = await Repository.TryHandleAsync(id, "Rejected", "人身攻擊", "備註B", OwnerId, AdminId, Clock.GetNow());

        Assert.Equal(1, first);   // 第一位成功
        Assert.Equal(0, second);  // 第二位 0 筆（WHERE Status='Pending' 已不成立）
    }

    [Fact]
    public async Task Handle_AdminOwnedTarget_ReportedMemberIsNull()
    {
        // 目標由 Admin 擁有 → 被檢舉會員排除 Admin，ReportedMemberID 應為 null
        var adminImage = new MidProject.Models.Image { UploadedByMemberID = AdminId, ImageURL = "/a.jpg", ImageType = "ReviewImage" };
        Db.Images.Add(adminImage);
        Db.SaveChanges();
        var imageId = adminImage.ImageID;
        Db.Entry(adminImage).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var id = AddReport(imageId: imageId);
        var outcome = await Service.HandleReportAsync(id, Dto(), AdminId);

        Assert.Equal(ReportHandleOutcome.Handled, outcome);
        var updated = await Service.GetByIdAsync(id);
        Assert.Null(updated!.ReportedMemberID);
    }
}
