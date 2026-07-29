using Microsoft.EntityFrameworkCore;

namespace MidProject.Data;

public static class DevelopmentTestDataSeeder
{
    public const string CommandArgument = "--seed-test-data";

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        await SeedData.InitializeAsync(serviceProvider);
        await PointsStoreDemoSeeder.InitializeAsync(serviceProvider);
        await ComprehensiveTestDataSeeder.SeedAsync(serviceProvider);
        await ReportsTestDataSeeder.SeedAsync(serviceProvider);
        await LogSummaryAsync(serviceProvider);
    }

    private static async Task LogSummaryAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MidProject.Data.DevelopmentTestDataSeeder");

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
            "日式料理", "台灣小吃", "咖哩", "蔬食", "深夜食堂", "已停用標籤"
        };
        var demoRestaurantNames = new[]
        {
            "信義夜食堂", "大安咖哩研究所", "松山蔬食小館", "中山深夜咖啡", "萬華老店（測試停用）"
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
                review => review.Content != null &&
                          review.Content.StartsWith("DEMO-REV-")),
            ["FavoriteFolders"] = demoFolderIds.Length,
            ["Favorites"] = await context.Favorites.CountAsync(
                favorite => demoFolderIds.Contains(favorite.FavoriteFolderID)),
            ["Reports"] = await context.Reports.CountAsync(
                report => report.Reason.StartsWith("DEMO-RPT-")),
            ["Notifications"] = await context.Notifications.CountAsync(
                notification => notification.Title.StartsWith("DEMO")),
            ["AvatarFrames"] = await context.AvatarFrames.CountAsync(
                frame => frame.Image != null &&
                         frame.Image.ImageURL.StartsWith("/uploads/AvatarFrame/demo-frame-")),
            ["MemberAvatarFrames"] = await context.MemberAvatarFrames.CountAsync(
                ownership => demoMemberIds.Contains(ownership.MemberID)),
            ["PointsTransactions"] = await context.PointsTransactions.CountAsync(
                transaction => transaction.Note != null &&
                               transaction.Note.StartsWith("DEMO-POINT-"))
        };
        const int expectedDemoRecordCount = 99;
        var demoRecordCount = demoCounts.Values.Sum();
        if (demoRecordCount != expectedDemoRecordCount)
        {
            throw new InvalidOperationException(
                $"Development test-data verification failed: expected {expectedDemoRecordCount} " +
                $"demo primary records, found {demoRecordCount}. " +
                string.Join(", ", demoCounts.Select(item => $"{item.Key}={item.Value}")));
        }

        logger.LogInformation(
            "Development test data ready; DemoPrimaryRecordCount={DemoPrimaryRecordCount}; " +
            "PrimaryRecordCount={PrimaryRecordCount}; DemoCounts={DemoCounts}; Counts={Counts}",
            demoRecordCount,
            counts.Values.Sum(),
            string.Join(", ", demoCounts.Select(item => $"{item.Key}={item.Value}")),
            string.Join(", ", counts.Select(item => $"{item.Key}={item.Value}")));
    }
}
