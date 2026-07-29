using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Repositories;
using MidProject.Services;
using MidProject.Services.IServices;

namespace Reports.Tests;

// 固定台灣時間的假時鐘
public sealed class FakeClock : ITaipeiClock
{
    private readonly DateTime _now;
    public FakeClock(DateTime now) => _now = now;
    public DateTime GetNow() => _now;
    public DateTime GetCurrentMinute() => NormalizeMinute(_now);
    public DateTime NormalizeMinute(DateTime v) => new(v.Year, v.Month, v.Day, v.Hour, v.Minute, 0);
}

// 監視用的通知模組替身：記錄呼叫、可設定回傳分類，供「通知整合」測試斷言
public sealed class SpyReportNotificationWindow : IReportNotificationWindow
{
    public List<ReportNotificationRequest> ReporterRequests { get; } = new();
    public List<ReportedMemberNotificationRequest> ReportedRequests { get; } = new();
    public ReportNotificationClassification NextResult { get; set; } = ReportNotificationClassification.Created;

    public Task<ReportNotificationResult> CreateOutcomeNotificationAsync(ReportNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ReporterRequests.Add(request);
        return Task.FromResult(new ReportNotificationResult(NextResult, NextResult == ReportNotificationClassification.Created ? 1 : null, null, "test"));
    }

    public Task<ReportNotificationResult> CreateReportedMemberNotificationAsync(ReportedMemberNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ReportedRequests.Add(request);
        return Task.FromResult(new ReportNotificationResult(NextResult, NextResult == ReportNotificationClassification.Created ? 1 : null, null, "test"));
    }
}

// 每個測試建立獨立的 SQL Server 測試資料庫（EnsureCreated 以正式模型建 schema，含 check 約束/篩選索引），
// 種好共用的「世界」（會員/餐廳/評論/圖片），測試各自新增自己的檢舉，結束後刪除資料庫確保隔離。
public abstract class ReportTestBase : IDisposable
{
    // 外部整合測試必須明確提供隔離 SQL Server；不得在原始碼保存共用帳密。
    private static readonly string ServerConnection =
        Environment.GetEnvironmentVariable("REPORTS_TEST_SERVER")
        ?? throw new InvalidOperationException(
            "REPORTS_TEST_SERVER is required and must target an isolated SQL Server test instance.");

    private readonly string _dbName = "ReportsTest_" + Guid.NewGuid().ToString("N");

    protected AppDbContext Db { get; }
    protected ReportRepository Repository { get; }
    protected ReportService Service { get; }
    protected SpyReportNotificationWindow Window { get; } = new();
    protected FakeClock Clock { get; } = new(new DateTime(2026, 7, 27, 10, 0, 0));

    protected int AdminId { get; private set; }
    protected int ReporterId { get; private set; }
    protected int OwnerId { get; private set; }
    protected int DeletedReporterId { get; private set; }
    protected int RestaurantId { get; private set; }
    protected int ReviewId { get; private set; }
    protected int ImageId { get; private set; }

    protected ReportTestBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer($"{ServerConnection};Database={_dbName}")
            .Options;
        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();
        SeedWorld();
        Repository = new ReportRepository(Db);
        Service = new ReportService(Repository, Window, Clock);
    }

    private void SeedWorld()
    {
        var level = new UserLevel { LevelName = "新手", MinExp = 0 };
        Db.UserLevels.Add(level);
        Db.SaveChanges();

        Member Make(string name, string role, bool deleted = false, string status = "Normal") => new()
        {
            UserName = name,
            NickName = name,
            Email = name + "@test.local",
            PasswordHash = "x",
            Role = role,
            Status = status,
            IsActive = true,
            IsLocked = false,
            IsDeleted = deleted,
            LevelID = level.LevelID
        };

        var admin = Make("admin", "Admin");
        var reporter = Make("reporter", "User");
        var owner = Make("owner", "User");
        var deletedReporter = Make("gone", "User", deleted: true, status: "Suspended");
        Db.Members.AddRange(admin, reporter, owner, deletedReporter);
        Db.SaveChanges();
        AdminId = admin.MemberID;
        ReporterId = reporter.MemberID;
        OwnerId = owner.MemberID;
        DeletedReporterId = deletedReporter.MemberID;

        var restaurant = new Restaurant
        {
            Name = "測試餐廳",
            City = "台北市",
            District = "中正區",
            DetailedAddress = "測試路 1 號",
            MemberID = owner.MemberID,
            AverageRating = 3,
            ReviewCount = 1
        };
        Db.Restaurants.Add(restaurant);
        Db.SaveChanges();
        RestaurantId = restaurant.RestaurantID;

        var review = new Review { MemberID = owner.MemberID, RestaurantID = restaurant.RestaurantID, Rating = 3, Content = "測試評論", Status = "Active" };
        Db.Reviews.Add(review);
        Db.SaveChanges();
        ReviewId = review.ReviewID;

        var image = new Image { UploadedByMemberID = owner.MemberID, ImageURL = "/uploads/test.jpg", ImageType = "ReviewImage" };
        Db.Images.Add(image);
        Db.SaveChanges();
        ImageId = image.ImageID;
    }

    // 建立一筆檢舉並回傳其 ID（建立後 detach，之後透過 repository 的 AsNoTracking 查詢會讀到最新狀態）
    protected int AddReport(
        string status = "Pending",
        int? reviewId = null, int? restaurantId = null, int? imageId = null,
        int? reporterId = null, int? reportedMemberId = null,
        string category = "人身攻擊", string reason = "測試原因",
        DateTime? createdAt = null, DateTime? handledAt = null)
    {
        var report = new Report
        {
            ReporterMemberID = reporterId ?? ReporterId,
            ReportedMemberID = reportedMemberId,
            ReviewID = reviewId,
            RestaurantID = restaurantId,
            ImageID = imageId,
            Reason = reason,
            Category = category,
            Status = status,
            CreatedAt = createdAt ?? Clock.GetNow(),
            HandledAt = handledAt
        };
        Db.Reports.Add(report);
        Db.SaveChanges();
        Db.Entry(report).State = EntityState.Detached;
        return report.ReportID;
    }

    public void Dispose()
    {
        try { Db.Database.EnsureDeleted(); } catch { /* best-effort cleanup */ }
        Db.Dispose();
        GC.SuppressFinalize(this);
    }
}
