using MidProject.Models;
using MidProject.Models.DTOs;
using MidProject.Repositories;
using MidProject.Services;
using Xunit;

namespace Reports.Tests;

public sealed class ReportServiceGuardTests
{
    private static readonly FakeClock Clock = new(new DateTime(2026, 7, 27, 10, 0, 0));

    [Fact]
    public async Task CreateReport_OwnContent_IsRejectedBeforeInsert()
    {
        var repository = new StubReportRepository { TargetOwnerMemberId = 7 };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateReportAsync(new ReportCreateDto
        {
            ReviewID = 3,
            Category = "人身攻擊",
            Reason = "測試原因"
        }, reporterMemberId: 7));

        Assert.Null(repository.AddedReport);
    }

    [Fact]
    public async Task HandleReport_InvalidCategory_IsRejectedBeforeUpdate()
    {
        var repository = new StubReportRepository();
        var service = CreateService(repository);

        var outcome = await service.HandleReportAsync(1, new ReportHandleDto
        {
            Status = "Approved",
            Category = "任意分類",
            AdminNote = "已確認"
        }, adminMemberId: 1);

        Assert.Equal(ReportHandleOutcome.InvalidCategory, outcome);
        Assert.Equal(0, repository.TryHandleCalls);
    }

    [Fact]
    public async Task HandleReport_SelfReport_IsRejectedBeforeUpdate()
    {
        var owner = new Member { MemberID = 7, Role = "User" };
        var repository = new StubReportRepository
        {
            Report = new Report
            {
                ReportID = 1,
                ReporterMemberID = owner.MemberID,
                Status = "Pending",
                Category = "人身攻擊",
                Reason = "測試原因",
                Review = new Review { Member = owner }
            }
        };
        var service = CreateService(repository);

        var outcome = await service.HandleReportAsync(1, new ReportHandleDto
        {
            Status = "Approved",
            Category = "人身攻擊",
            AdminNote = "已確認"
        }, adminMemberId: 1);

        Assert.Equal(ReportHandleOutcome.SelfReportNotAllowed, outcome);
        Assert.Equal(0, repository.TryHandleCalls);
    }

    [Fact]
    public async Task GetReports_UsesTaipeiClockForSortAndDisplay()
    {
        var repository = new StubReportRepository
        {
            QueryResult = new PagedResult<Report>
            {
                Items = new List<Report>
                {
                    new()
                    {
                        ReportID = 1,
                        ReporterMemberID = 2,
                        Status = "Pending",
                        Category = "人身攻擊",
                        Reason = "測試原因",
                        CreatedAt = Clock.GetNow().AddDays(-2)
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 10
            }
        };
        var service = CreateService(repository);

        var result = await service.GetReportsAsync(new ReportQueryParams());

        Assert.Equal(Clock.GetNow().Date, repository.QueryToday);
        Assert.Equal(2, Assert.Single(result.Items).ProcessingDays);
    }

    [Fact]
    public async Task Dashboard_UsesTaipeiClockForCurrentMonth()
    {
        var repository = new StubReportRepository();
        var service = CreateService(repository);

        await service.GetDashboardAsync();

        Assert.Equal(new DateTime(2026, 7, 1), repository.PendingSince);
        Assert.Equal(new DateTime(2025, 8, 1), repository.StatusDatesSince);
    }

    private static ReportService CreateService(StubReportRepository repository) =>
        new(repository, new SpyReportNotificationWindow(), Clock);

    private sealed class StubReportRepository : IReportRepository
    {
        public int? TargetOwnerMemberId { get; init; }
        public Report? Report { get; init; }
        public Report? AddedReport { get; private set; }
        public PagedResult<Report> QueryResult { get; init; } = new();
        public DateTime? QueryToday { get; private set; }
        public DateTime? PendingSince { get; private set; }
        public DateTime? StatusDatesSince { get; private set; }
        public int TryHandleCalls { get; private set; }

        public Task<PagedResult<Report>> GetReportsAsync(ReportQueryParams query, DateTime today)
        {
            QueryToday = today;
            return Task.FromResult(QueryResult);
        }

        public Task<Report?> GetByIdAsync(int reportId) => Task.FromResult(Report);

        public Task AddAsync(Report report)
        {
            AddedReport = report;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync() => Task.CompletedTask;

        public Task<int?> GetTargetOwnerMemberIdAsync(int? restaurantId, int? reviewId, int? imageId) =>
            Task.FromResult(TargetOwnerMemberId);

        public Task<int> TryHandleAsync(
            int reportId,
            string status,
            string category,
            string adminNote,
            int? reportedMemberId,
            int adminMemberId,
            DateTime handledAt)
        {
            TryHandleCalls++;
            return Task.FromResult(1);
        }

        public Task<List<Notification>> GetNotificationsByReportAsync(int reportId) =>
            Task.FromResult(new List<Notification>());

        public Task<(int pending, int approved, int rejected)> GetStatusCountsAsync() =>
            Task.FromResult((0, 0, 0));

        public Task<int> GetPendingCountSinceAsync(DateTime since)
        {
            PendingSince = since;
            return Task.FromResult(0);
        }

        public Task<List<ReportStatusDatePoint>> GetStatusDatesSinceAsync(DateTime since)
        {
            StatusDatesSince = since;
            return Task.FromResult(new List<ReportStatusDatePoint>());
        }
    }
}
