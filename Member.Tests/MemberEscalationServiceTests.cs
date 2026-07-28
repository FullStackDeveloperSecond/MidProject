using MidProject.Data;
using MidProject.Models;
using MidProject.Services;
using Xunit;
using MemberModel = MidProject.Models.Member;

namespace Member.Tests;

// 覆蓋缺失清單「停權與解除」的自動懲處部分：受理檢舉次數對應的懲處等級、
// 同一次數不重複套用（WarningCount 檢查點）、自動停權時 DeletedBy 要記錄實際核准檢舉的管理員。
public class MemberEscalationServiceTests
{
    private static (MemberEscalationService Service, AppDbContext Context, FakeTaipeiClock Clock) CreateService()
    {
        var context = InMemoryDbContextFactory.Create();
        var clock = new FakeTaipeiClock();
        var service = new MemberEscalationService(context, clock);
        return (service, context, clock);
    }

    private static async Task<MemberModel> SeedMemberAsync(AppDbContext context, string status = "Normal", int warningCount = 0)
    {
        var member = new MemberModel
        {
            UserName = "tester",
            Email = "tester@example.com",
            PasswordHash = "hash",
            Role = "User",
            Status = status,
            WarningCount = warningCount,
            LevelID = 1,
            IsActive = true
        };
        context.Members.Add(member);
        await context.SaveChangesAsync();
        return member;
    }

    private static async Task AddApprovedReportsAsync(AppDbContext context, int reportedMemberId, int count, int? handledByMemberId = null)
    {
        for (var i = 0; i < count; i++)
        {
            context.Reports.Add(new Report
            {
                ReporterMemberID = 1,
                ReportedMemberID = reportedMemberId,
                Reason = "測試檢舉",
                Category = "其他",
                Status = "Approved",
                HandledByMemberID = handledByMemberId,
                HandledAt = DateTime.UtcNow.AddMinutes(i),
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData(1, "Warning")]
    [InlineData(2, "Muted")]
    [InlineData(3, "Muted")]
    [InlineData(4, "Suspended")]
    public async Task RunOnceAsync_EscalatesToCorrectStatus(int approvedCount, string expectedStatus)
    {
        var (service, context, _) = CreateService();
        var member = await SeedMemberAsync(context);
        await AddApprovedReportsAsync(context, member.MemberID, approvedCount);

        await service.RunOnceAsync();

        var updated = await context.Members.FindAsync(member.MemberID);
        Assert.Equal(expectedStatus, updated!.Status);
        Assert.Equal(approvedCount, updated.WarningCount);
    }

    [Fact]
    public async Task RunOnceAsync_TwoApprovedReports_SetsSevenDayPenalty()
    {
        var (service, context, clock) = CreateService();
        var member = await SeedMemberAsync(context);
        await AddApprovedReportsAsync(context, member.MemberID, 2);

        await service.RunOnceAsync();

        var updated = await context.Members.FindAsync(member.MemberID);
        Assert.Equal(clock.GetNow().AddDays(7), updated!.PenaltyEndAt);
    }

    [Fact]
    public async Task RunOnceAsync_ThreeApprovedReports_SetsOneMonthPenalty()
    {
        var (service, context, clock) = CreateService();
        var member = await SeedMemberAsync(context);
        await AddApprovedReportsAsync(context, member.MemberID, 3);

        await service.RunOnceAsync();

        var updated = await context.Members.FindAsync(member.MemberID);
        Assert.Equal(clock.GetNow().AddMonths(1), updated!.PenaltyEndAt);
    }

    [Fact]
    public async Task RunOnceAsync_SameApprovedCountAsLastRun_DoesNotReapplyOrExtendPenalty()
    {
        var (service, context, clock) = CreateService();
        // WarningCount 已經等於目前的受理次數，代表懲處排程已經套用過這個次數了
        var member = await SeedMemberAsync(context, status: "Muted", warningCount: 2);
        member.PenaltyEndAt = clock.GetNow().AddDays(3); // 假設是上次套用時算出來、還沒到期的期限
        await context.SaveChangesAsync();
        await AddApprovedReportsAsync(context, member.MemberID, 2);

        await service.RunOnceAsync();

        var updated = await context.Members.FindAsync(member.MemberID);
        // 期限應該維持原樣，不會因為排程又跑一次就被往後延
        Assert.Equal(clock.GetNow().AddDays(3), updated!.PenaltyEndAt);
    }

    [Fact]
    public async Task RunOnceAsync_FourApprovedReports_SuspendsAndAttributesDeletedByToReportHandler()
    {
        var (service, context, clock) = CreateService();
        var member = await SeedMemberAsync(context);
        const int handlingAdminId = 7;
        await AddApprovedReportsAsync(context, member.MemberID, 4, handledByMemberId: handlingAdminId);

        await service.RunOnceAsync();

        var updated = await context.Members.FindAsync(member.MemberID);
        Assert.Equal("Suspended", updated!.Status);
        Assert.True(updated.IsDeleted);
        Assert.Equal(clock.GetNow(), updated.DeletedAt);
        Assert.Equal(handlingAdminId, updated.DeletedBy);
    }

    [Fact]
    public async Task RunOnceAsync_PenaltyExpired_ResetsToNormal()
    {
        var (service, context, clock) = CreateService();
        var member = await SeedMemberAsync(context, status: "Muted", warningCount: 2);
        member.PenaltyEndAt = clock.GetNow().AddMinutes(-1); // 已經到期
        await context.SaveChangesAsync();

        await service.RunOnceAsync();

        var updated = await context.Members.FindAsync(member.MemberID);
        Assert.Equal("Normal", updated!.Status);
        Assert.Null(updated.PenaltyEndAt);
    }

    [Fact]
    public async Task RunOnceAsync_DeletedMember_IsNeverTouched()
    {
        var (service, context, _) = CreateService();
        var member = await SeedMemberAsync(context, status: "Deleted");
        await AddApprovedReportsAsync(context, member.MemberID, 4);

        await service.RunOnceAsync();

        var updated = await context.Members.FindAsync(member.MemberID);
        Assert.Equal("Deleted", updated!.Status);
    }
}
