using Microsoft.EntityFrameworkCore;

namespace MidProject.Data;

public static class DemoDataSeeder
{
    public const string CommandArgument = "--seed-demo-data";

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        await SeedData.InitializeAsync(serviceProvider);
        await PointsStoreDemoSeeder.InitializeAsync(serviceProvider);
        await ComprehensiveDemoDataSeeder.SeedAsync(serviceProvider);
        await ReportsDemoDataSeeder.SeedAsync(serviceProvider);
        await LogSummaryAsync(serviceProvider);
    }

    private static async Task LogSummaryAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MidProject.Data.DemoDataSeeder");

        var counts = new Dictionary<string, int>
        {
            ["UserLevels"] = await context.UserLevels.CountAsync(),
            ["Members"] = await context.Members.CountAsync(),
            ["Tags"] = await context.Tags.CountAsync(),
            ["Restaurants"] = await context.Restaurants.CountAsync(),
            ["Images"] = await context.Images.CountAsync(),
            ["Reviews"] = await context.Reviews.CountAsync(),
            ["FavoriteFolders"] = await context.FavoriteFolders.CountAsync(),
            ["Favorites"] = await context.Favorites.CountAsync(),
            ["Reports"] = await context.Reports.CountAsync(),
            ["Notifications"] = await context.Notifications.CountAsync(),
            ["AvatarFrames"] = await context.AvatarFrames.CountAsync(),
            ["MemberAvatarFrames"] = await context.MemberAvatarFrames.CountAsync(),
            ["PointsTransactions"] = await context.PointsTransactions.CountAsync()
        };

        var demoMemberIds = await context.Members
            .Where(member => member.Email.StartsWith("demo."))
            .Select(member => member.MemberID)
            .ToArrayAsync();
        var demoFolderIds = await context.FavoriteFolders
            .Where(folder => demoMemberIds.Contains(folder.MemberID))
            .Select(folder => folder.FavoriteFolderID)
            .ToArrayAsync();
        var demoTagNames = new[]
        {
            "日式料理", "台灣小吃", "咖哩", "咖啡廳", "蔬食", "深夜食堂", "季節限定"
        };
        var demoRestaurantNames = new[]
        {
            "信義夜食堂", "大安咖哩研究所", "松山蔬食小館", "中山深夜咖啡", "萬華古早味麵店"
        };
        var demoRestaurantIds = await context.Restaurants
            .Where(restaurant => demoRestaurantNames.Contains(restaurant.Name))
            .Select(restaurant => restaurant.RestaurantID)
            .ToArrayAsync();
        var demoNotificationTitles = new[]
        {
            "收藏清單提醒", "本週美食推薦", "帳號安全提醒", "平台功能更新",
            "社群活動開跑", "社群規範提醒", "等級升級專屬好禮", "營運公告"
        };
        var demoPointNotes = new[]
        {
            "夏季美食募集活動獎勵", "兌換城市星夜外框", "重複發放活動點數沖銷",
            "優質評論獎勵", "兌換晨光暖橙外框", "客服更正點數紀錄"
        };
        var demoCounts = new Dictionary<string, int>
        {
            ["Members"] = demoMemberIds.Length,
            ["Tags"] = await context.Tags.CountAsync(tag => demoTagNames.Contains(tag.TagName)),
            ["Restaurants"] = await context.Restaurants.CountAsync(
                restaurant => demoRestaurantNames.Contains(restaurant.Name)),
            ["Images"] = await context.Images.CountAsync(
                image => image.ImageURL.Contains("/demo-")),
            ["Reviews"] = await context.Reviews.CountAsync(
                review => demoMemberIds.Contains(review.MemberID)),
            ["FavoriteFolders"] = demoFolderIds.Length,
            ["Favorites"] = await context.Favorites.CountAsync(
                favorite => demoFolderIds.Contains(favorite.FavoriteFolderID)),
            ["Reports"] = await context.Reports.CountAsync(
                report => report.Reason.StartsWith("案件月份 ")),
            ["Notifications"] = await context.Notifications.CountAsync(
                notification => demoNotificationTitles.Contains(notification.Title) ||
                                notification.SourceReportID != null),
            ["AvatarFrames"] = await context.AvatarFrames.CountAsync(
                frame => frame.Image != null &&
                         frame.Image.ImageURL.StartsWith("/uploads/AvatarFrame/demo-frame-")),
            ["MemberAvatarFrames"] = await context.MemberAvatarFrames.CountAsync(
                ownership => demoMemberIds.Contains(ownership.MemberID)),
            ["PointsTransactions"] = await context.PointsTransactions.CountAsync(
                transaction => transaction.Note != null &&
                               demoPointNotes.Contains(transaction.Note))
        };
        const int expectedDemoRecordCount = 100;
        var demoRecordCount = demoCounts.Values.Sum();
        if (demoRecordCount != expectedDemoRecordCount)
        {
            throw new InvalidOperationException(
                $"Demo data verification failed: expected {expectedDemoRecordCount} " +
                $"primary demo records, found {demoRecordCount}. " +
                string.Join(", ", demoCounts.Select(item => $"{item.Key}={item.Value}")));
        }

        var demoReviewIds = await context.Reviews
            .Where(review => demoMemberIds.Contains(review.MemberID))
            .Select(review => review.ReviewID)
            .ToArrayAsync();
        var relationshipCounts = new Dictionary<string, int>
        {
            ["BusinessHours"] = await context.BusinessHours.CountAsync(
                hour => demoRestaurantIds.Contains(hour.RestaurantID)),
            ["RestaurantTags"] = await context.RestaurantTags.CountAsync(
                link => demoRestaurantIds.Contains(link.RestaurantID)),
            ["RestaurantImages"] = await context.RestaurantImages.CountAsync(
                link => demoRestaurantIds.Contains(link.RestaurantID)),
            ["ReviewImages"] = await context.ReviewImages.CountAsync(
                link => demoReviewIds.Contains(link.ReviewID)),
            ["RestaurantReports"] = await context.Reports.CountAsync(
                report => report.Reason.StartsWith("案件月份 ") &&
                          report.RestaurantID != null),
            ["ReviewReports"] = await context.Reports.CountAsync(
                report => report.Reason.StartsWith("案件月份 ") &&
                          report.ReviewID != null),
            ["ImageReports"] = await context.Reports.CountAsync(
                report => report.Reason.StartsWith("案件月份 ") &&
                          report.ImageID != null)
        };
        var expectedRelationshipCounts = new Dictionary<string, int>
        {
            ["BusinessHours"] = 36,
            ["RestaurantTags"] = 10,
            ["RestaurantImages"] = 8,
            ["ReviewImages"] = 4,
            ["RestaurantReports"] = 4,
            ["ReviewReports"] = 4,
            ["ImageReports"] = 4
        };
        var invalidRelationships = relationshipCounts
            .Where(item => item.Value != expectedRelationshipCounts[item.Key])
            .ToArray();
        if (invalidRelationships.Length > 0)
        {
            throw new InvalidOperationException(
                "Demo relationship verification failed: " +
                string.Join(", ", invalidRelationships.Select(
                    item => $"{item.Key}={item.Value}, expected={expectedRelationshipCounts[item.Key]}")));
        }

        var imageUrls = await context.Images
            .Where(image => image.ImageURL.Contains("/demo-"))
            .Select(image => image.ImageURL)
            .ToArrayAsync();
        var missingImageUrls = imageUrls
            .Where(url => !File.Exists(Path.Combine(
                environment.WebRootPath,
                url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
        if (missingImageUrls.Length > 0)
        {
            throw new InvalidOperationException(
                "Demo image verification failed; missing files: " +
                string.Join(", ", missingImageUrls));
        }

        logger.LogInformation(
            "Demo presentation data ready; DemoPrimaryRecordCount={DemoPrimaryRecordCount}; " +
            "PrimaryRecordCount={PrimaryRecordCount}; DemoCounts={DemoCounts}; " +
            "RelationshipCounts={RelationshipCounts}; ImageFileCount={ImageFileCount}; Counts={Counts}",
            demoRecordCount,
            counts.Values.Sum(),
            string.Join(", ", demoCounts.Select(item => $"{item.Key}={item.Value}")),
            string.Join(", ", relationshipCounts.Select(item => $"{item.Key}={item.Value}")),
            imageUrls.Length,
            string.Join(", ", counts.Select(item => $"{item.Key}={item.Value}")));
    }
}
