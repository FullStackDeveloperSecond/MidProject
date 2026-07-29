using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Services.IServices;

namespace MidProject.Data;

/// <summary>
/// 建立固定 12 個月、三種狀態、三種目標的檢舉資料。
/// 每筆使用穩定識別字，可重複執行而不重複新增。
/// </summary>
public static class ReportsDemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ITaipeiClock>();
        var now = clock.GetNow();

        var members = await context.Members
            .Where(member => !member.IsDeleted)
            .OrderBy(member => member.MemberID)
            .ToListAsync();
        var admin = members.First(member => member.Role == "Admin");
        var reporters = members.Where(member => member.Role != "Admin").ToList();
        var adminIds = members
            .Where(member => member.Role == "Admin")
            .Select(member => member.MemberID)
            .ToHashSet();
        var restaurants = await context.Restaurants
            .Where(restaurant => !restaurant.IsDeleted)
            .OrderBy(restaurant => restaurant.RestaurantID)
            .ToListAsync();
        var reviews = await context.Reviews
            .Where(review => !review.IsDeleted)
            .OrderBy(review => review.ReviewID)
            .ToListAsync();
        var images = await context.Images
            .Where(image => !image.IsDeleted)
            .OrderBy(image => image.ImageID)
            .ToListAsync();

        if (reporters.Count == 0 ||
            restaurants.Count == 0 ||
            reviews.Count == 0 ||
            images.Count == 0)
        {
            return;
        }

        var categories = new[]
        {
            "不實資訊", "廣告洗版", "人身攻擊", "仇恨言論", "色情內容", "垃圾訊息"
        };
        var statuses = new[] { "Pending", "Approved", "Rejected" };
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var earliestMonth = monthStart.AddMonths(-11);
        var scenarioReports = new List<Report>();

        for (var index = 0; index < 12; index++)
        {
            var bucketMonth = earliestMonth.AddMonths(index);
            var identifier = $"案件月份 {bucketMonth:yyyyMM}";
            var existingReport = await context.Reports
                .FirstOrDefaultAsync(report => report.Reason.StartsWith(identifier));
            if (existingReport is not null)
            {
                scenarioReports.Add(existingReport);
                continue;
            }

            int? restaurantId = null;
            int? reviewId = null;
            int? imageId = null;
            int? targetOwnerId;
            switch (index % 3)
            {
                case 0:
                    var restaurant = restaurants[index % restaurants.Count];
                    restaurantId = restaurant.RestaurantID;
                    targetOwnerId = restaurant.MemberID;
                    break;
                case 1:
                    var review = reviews[index % reviews.Count];
                    reviewId = review.ReviewID;
                    targetOwnerId = review.MemberID;
                    break;
                default:
                    var image = images[index % images.Count];
                    imageId = image.ImageID;
                    targetOwnerId = image.UploadedByMemberID;
                    break;
            }

            var eligibleReporters = reporters
                .Where(member => member.MemberID != targetOwnerId)
                .ToList();
            if (eligibleReporters.Count == 0)
            {
                continue;
            }

            var status = statuses[index % statuses.Length];
            var createdAt = new DateTime(
                bucketMonth.Year,
                bucketMonth.Month,
                Math.Min(5 + index, DateTime.DaysInMonth(bucketMonth.Year, bucketMonth.Month)),
                10 + index % 8,
                15,
                0,
                DateTimeKind.Unspecified);
            if (createdAt > now)
            {
                createdAt = now;
            }

            var report = new Report
            {
                ReporterMemberID = eligibleReporters[index % eligibleReporters.Count].MemberID,
                RestaurantID = restaurantId,
                ReviewID = reviewId,
                ImageID = imageId,
                Reason = $"{identifier}｜使用者回報疑似{categories[index % categories.Length]}內容，請協助查核。",
                Category = categories[index % categories.Length],
                Status = status,
                CreatedAt = createdAt
            };

            if (status != "Pending")
            {
                report.HandledAt = createdAt.AddDays(1) > now ? now : createdAt.AddDays(1);
                report.HandledByMemberID = admin.MemberID;
                report.ReportedMemberID = targetOwnerId.HasValue &&
                                          !adminIds.Contains(targetOwnerId.Value)
                    ? targetOwnerId
                    : null;
                report.AdminNote = status == "Approved"
                    ? "已完成內容查核，確認違反社群規範。"
                    : "已完成內容查核，目前無足夠事證認定違規。";
            }

            context.Reports.Add(report);
            scenarioReports.Add(report);
        }

        await context.SaveChangesAsync();

        foreach (var report in scenarioReports.Where(report => report.Status != "Pending"))
        {
            if (await context.Notifications.AnyAsync(notification =>
                    notification.SourceReportID == report.ReportID &&
                    notification.SourceReportOutcome == report.Status &&
                    notification.MemberID == report.ReporterMemberID))
            {
                continue;
            }

            var handledAt = report.HandledAt ?? now;
            context.Notifications.Add(new Notification
            {
                MemberID = report.ReporterMemberID,
                NotificationType = "Personal",
                Title = $"檢舉案件{(report.Status == "Approved" ? "成立" : "駁回")}通知",
                Content = $"您提交的檢舉案件 #{report.ReportID} 已完成審核，感謝您協助維護社群品質。",
                ScheduledAt = handledAt,
                SentAt = handledAt,
                IsSent = true,
                CreatedAt = handledAt,
                CreatedBy = admin.MemberID,
                SourceReportID = report.ReportID,
                SourceReportOutcome = report.Status
            });
        }

        await context.SaveChangesAsync();
        await RefreshReportCountersAsync(context);
    }

    private static async Task RefreshReportCountersAsync(AppDbContext context)
    {
        var reviews = await context.Reviews.ToListAsync();
        var reviewCounts = await context.Reports
            .Where(report => !report.IsDeleted && report.ReviewID != null)
            .GroupBy(report => report.ReviewID!.Value)
            .Select(group => new { ReviewId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ReviewId, item => item.Count);
        foreach (var review in reviews)
        {
            review.ReportCount = reviewCounts.GetValueOrDefault(review.ReviewID);
        }

        var demoMembers = await context.Members
            .Where(member => member.Email.StartsWith("demo."))
            .ToListAsync();
        var approvedCounts = await context.Reports
            .Where(report => !report.IsDeleted &&
                             report.Status == "Approved" &&
                             report.ReportedMemberID != null)
            .GroupBy(report => report.ReportedMemberID!.Value)
            .Select(group => new { MemberId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.MemberId, item => item.Count);
        foreach (var member in demoMembers)
        {
            member.WarningCount = approvedCounts.GetValueOrDefault(member.MemberID);
        }

        await context.SaveChangesAsync();
    }
}
