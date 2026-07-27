using MidProject.Models.DTOs;
using MidProject.Services.IServices;
using Xunit;

namespace Reports.Tests;

// 涵蓋：Reports 與通知模組（IReportNotificationWindow）的整合——
// 交給 window（不直接寫 Notification）、待處理案件按儲存才定案再通知、各種結果訊息、重複、被檢舉會員僅成立時通知
public sealed class ReportNotificationIntegrationTests : ReportTestBase
{
    private NotifyReporterDto Notify(string status = "Approved")
        => new()
        {
            Title = "【檢舉結果通知】t",
            Content = "c",
            HandleStatus = status,
            HandleCategory = "人身攻擊",
            HandleAdminNote = "已確認違規"
        };

    [Fact]
    public async Task NotifyReporter_PendingReport_HandlesThenRoutesToWindow()
    {
        var id = AddReport(reviewId: ReviewId);

        var result = await Service.NotifyReporterAsync(id, Notify("Approved"), AdminId);

        Assert.True(result.Success);
        // 待處理案件在按儲存時才定案
        var updated = await Service.GetByIdAsync(id);
        Assert.Equal("Approved", updated!.Status);
        // 交給通知模組（而非 Reports 直接寫 Notification），且參數正確
        var req = Assert.Single(Window.ReporterRequests);
        Assert.Equal(id, req.ReportID);
        Assert.Equal(ReporterId, req.ReporterMemberID);
        Assert.Equal("Approved", req.Outcome);
        Assert.Equal("c", req.Content); // 使用管理員編輯後的內容
    }

    [Fact]
    public async Task NotifyReporter_WithoutHandleDecision_OnPending_Fails()
    {
        var id = AddReport(reviewId: ReviewId);

        // 沒有帶處理決定 → 維持待處理、不通知
        var result = await Service.NotifyReporterAsync(id, new NotifyReporterDto { Title = "t", Content = "c" }, AdminId);

        Assert.False(result.Success);
        Assert.Empty(Window.ReporterRequests);
        var updated = await Service.GetByIdAsync(id);
        Assert.Equal("Pending", updated!.Status);
    }

    [Fact]
    public async Task NotifyReporter_WindowReportsMemberDeleted_ReturnsRecipientUnavailable_ButReportHandled()
    {
        var id = AddReport(reviewId: ReviewId, reporterId: DeletedReporterId);
        Window.NextResult = ReportNotificationClassification.MemberDeleted;

        var result = await Service.NotifyReporterAsync(id, Notify("Approved"), AdminId);

        Assert.False(result.Success);
        Assert.True(result.RecipientUnavailable);
        Assert.NotNull(result.Message);
        // 收件人已刪除，通知未送出，但檢舉仍完成處理
        var updated = await Service.GetByIdAsync(id);
        Assert.Equal("Approved", updated!.Status);
    }

    [Fact]
    public async Task NotifyReporter_WindowReportsAlreadyExists_ReturnsAlreadyNotified()
    {
        var id = AddReport(reviewId: ReviewId);
        Window.NextResult = ReportNotificationClassification.AlreadyExists;

        var result = await Service.NotifyReporterAsync(id, Notify("Approved"), AdminId);

        Assert.False(result.Success);
        Assert.True(result.AlreadyNotified);
    }

    [Fact]
    public async Task NotifyReporter_WindowReportsAdminInvalid_ReturnsDistinctMessage()
    {
        var id = AddReport(reviewId: ReviewId);
        Window.NextResult = ReportNotificationClassification.AdminInvalid;

        var result = await Service.NotifyReporterAsync(id, Notify("Approved"), AdminId);

        Assert.False(result.Success);
        Assert.False(result.RecipientUnavailable);
        Assert.False(result.AlreadyNotified);
        Assert.Contains("管理員", result.Message);
    }

    [Fact]
    public async Task NotifyReportedMember_OnApproved_RoutesToWindow()
    {
        var id = AddReport(reviewId: ReviewId);

        var result = await Service.NotifyReportedMemberAsync(id, Notify("Approved"), AdminId);

        Assert.True(result.Success);
        var req = Assert.Single(Window.ReportedRequests);
        Assert.Equal(id, req.ReportID);
        Assert.Equal(OwnerId, req.ReportedMemberID);   // 收件＝被檢舉會員（評論擁有者）
    }

    [Fact]
    public async Task NotifyReportedMember_OnRejected_DoesNotNotify()
    {
        var id = AddReport(reviewId: ReviewId);

        // 駁回檢舉不需通知被檢舉會員
        var result = await Service.NotifyReportedMemberAsync(id, Notify("Rejected"), AdminId);

        Assert.False(result.Success);
        Assert.Empty(Window.ReportedRequests);
    }
}
