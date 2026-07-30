using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Services;
using MidProject.Services.IServices;

namespace MidProject.Data;

/// <summary>
/// Rebuilds the local presentation database with deterministic, relationship-complete data.
/// This is intentionally separate from the small development seeder because it deletes every
/// application row before inserting the formal presentation dataset.
/// </summary>
public static class FormalDemoDataSeeder
{
    public const string CommandArgument = "--reset-formal-demo-data";
    public const string ConfirmationArgument = "--confirm-reset-midprojectdb";
    public const string RefreshMemberAvatarsCommandArgument = "--refresh-formal-demo-avatars";
    public const string RefreshMemberAvatarsConfirmationArgument = "--confirm-refresh-midprojectdb";
    public const string RefreshDashboardDistributionCommandArgument =
        "--refresh-formal-dashboard-distribution";
    public const string RefreshDashboardDistributionConfirmationArgument =
        "--confirm-refresh-dashboard-distribution";

    private const string RequiredDatabaseName = "MidProjectDb";
    private const string AdminEmail = "admin@example.com";
    private const int ExpectedRecordCount = 10_000;
    private const int ExpectedPhysicalImageCount = 120;
    private const int ExpectedCustomMemberAvatarCount = 560;

    private static readonly int[] MemberRecentMonthlyCounts = [28, 44, 67, 31, 82, 56];
    private static readonly int[] RestaurantRecentMonthlyCounts = [8, 19, 12, 34, 15, 27];
    private static readonly int[] ReviewRecentMonthlyCounts = [35, 68, 51, 120, 83, 144];
    private static readonly int[] ReportRecentMonthlyCounts = [11, 24, 9, 36, 18, 42];
    private static readonly int[] PopularRestaurantIndexes = [100, 101, 103, 104, 105, 106, 107, 108];
    private static readonly int[] PopularRestaurantFavoriteCounts = [118, 92, 70, 52, 38, 27, 18, 11];
    private static readonly int[] PopularRestaurantReviewCounts = [148, 121, 96, 74, 57, 41, 29, 19];
    private static readonly int[] PopularRestaurantBaseRatings = [5, 4, 4, 3, 5, 2, 4, 3];

    private static readonly string[] ApplicationTables =
    [
        "Notifications", "PointsTransactions", "MemberAvatarFrames", "Favorites",
        "FavoriteFolders", "Reports", "ReviewImages", "Reviews", "RestaurantImages",
        "RestaurantTags", "BusinessHours", "AvatarFrames", "Restaurants", "Tags",
        "Members", "Images", "UserLevels"
    ];

    private static readonly string[] IdentityTables =
    [
        "Notifications", "PointsTransactions", "MemberAvatarFrames", "Favorites",
        "FavoriteFolders", "Reports", "Reviews", "BusinessHours", "AvatarFrames",
        "Restaurants", "Tags", "Members", "Images", "UserLevels"
    ];

    private static readonly string[] LevelNames =
        ["新食客", "尋味人", "品味家", "老饕客", "鑑味師", "食之神"];

    private static readonly string[] TagNames =
    [
        "台灣小吃", "日式料理", "韓式料理", "義式料理", "法式料理",
        "泰式料理", "越式料理", "港式料理", "川味料理", "江浙料理",
        "早午餐", "咖啡廳", "甜點", "麵食", "火鍋",
        "燒肉", "居酒屋", "海鮮", "牛排", "蔬食",
        "素食", "宵夜", "親子友善", "寵物友善", "約會餐廳",
        "聚餐推薦", "平價美食", "精緻餐飲", "景觀餐廳", "老字號",
        "新開幕", "排隊美食", "外帶推薦", "無障礙", "有包廂",
        "捷運周邊", "在地特色", "季節限定", "健康餐", "異國料理"
    ];

    private static readonly string[] Categories =
        ["不實資訊", "廣告洗版", "人身攻擊", "仇恨言論", "色情內容", "垃圾訊息"];

    private static readonly (string City, string[] Districts)[] Locations =
    [
        ("台北市", ["中正區", "大同區", "中山區", "松山區", "大安區", "萬華區", "信義區", "士林區", "北投區", "內湖區", "南港區", "文山區"]),
        ("新北市", ["板橋區", "新店區", "中和區", "永和區", "三重區", "新莊區", "淡水區", "汐止區"]),
        ("桃園市", ["桃園區", "中壢區", "龜山區", "八德區", "蘆竹區"]),
        ("台中市", ["中區", "西區", "北區", "南屯區", "西屯區", "北屯區"]),
        ("台南市", ["中西區", "東區", "南區", "北區", "安平區"]),
        ("高雄市", ["新興區", "前金區", "苓雅區", "左營區", "鼓山區", "三民區"])
    ];

    private static readonly string[] RestaurantPrefixes =
        ["拾味", "日和", "青禾", "暖巷", "山海", "小滿", "暮光", "木子", "晴川", "稻香"];

    private static readonly string[] RestaurantSuffixes =
        ["食堂", "小館", "料理所", "餐酒館", "咖啡室", "茶屋", "麵舖", "飯館", "廚房", "甜點坊"];

    private static readonly string[] Surnames =
        ["陳", "林", "黃", "張", "李", "王", "吳", "劉", "蔡", "楊", "許", "鄭", "謝", "郭", "洪"];

    private static readonly string[] GivenNames =
        ["雅雯", "子謙", "怡君", "柏翰", "家豪", "品妤", "冠廷", "雨潔", "承恩", "思妤", "宇翔", "佳穎", "俊傑", "詠晴", "哲維"];

    private static readonly string[] ReviewTexts =
    [
        "餐點調味平衡，服務人員親切，整體用餐節奏很舒服。",
        "食材新鮮、份量剛好，適合與朋友一起聚餐。",
        "環境整潔明亮，尖峰時段稍微需要等候。",
        "招牌料理香氣十足，下次會想再嘗試其他品項。",
        "交通方便，從捷運站步行幾分鐘就能抵達。",
        "甜點表現出色，飲品甜度也可以依照喜好調整。",
        "座位舒適且空間寬敞，家庭聚餐也很合適。",
        "價格合理，餐點品質與份量符合期待。",
        "晚間氣氛很好，店內音樂不會影響交談。",
        "出餐速度穩定，店員對菜單內容介紹得很清楚。"
    ];

    public static async Task ResetAndSeedAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ITaipeiClock>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MidProject.Data.FormalDemoDataSeeder");

        var databaseName = EnsureRequiredDatabase(context, "Formal data reset");

        var adminPassword = configuration["FormalDemoData:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "FormalDemoData:AdminPassword is required. Store it in .NET User Secrets; " +
                "never place the presentation password in source control.");
        }

        var assetRoot = Path.Combine(environment.ContentRootPath, "FormalDemoAssets");
        var assetFiles = Directory.Exists(assetRoot)
            ? Directory.GetFiles(assetRoot, "*.jpg", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
        if (assetFiles.Length != ExpectedPhysicalImageCount)
        {
            throw new InvalidOperationException(
                $"Formal image assets are incomplete: expected {ExpectedPhysicalImageCount} JPG files, " +
                $"found {assetFiles.Length} in '{assetRoot}'.");
        }

        var now = clock.GetNow();
        logger.LogWarning(
            "Formal presentation reset starting; Database={Database}; Existing data will be deleted without backup.",
            databaseName);

        await ClearApplicationDataAsync(context);
        await AssertDatabaseEmptyAsync(context);
        ReplaceRuntimeImages(environment, assetFiles);

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var levels = await SeedLevelsAsync(context);
            var (admin, users, approvalOwners) =
                await SeedMembersAsync(context, levels, adminPassword, now);
            var tags = await SeedTagsAsync(context, admin.MemberID, now);
            var restaurants = await SeedRestaurantsAsync(
                context, users, approvalOwners, admin.MemberID, now);
            await SeedBusinessHoursAsync(context, restaurants);
            await SeedRestaurantTagsAsync(context, restaurants, tags);
            var images = await SeedImagesAsync(
                context, users, approvalOwners, admin.MemberID, now);
            await SeedRestaurantImagesAsync(context, restaurants, images);
            var reviews = await SeedReviewsAsync(
                context, users, approvalOwners, restaurants, admin.MemberID, now);
            await SeedReviewImagesAsync(context, reviews, images);
            var folders = await SeedFavoriteFoldersAsync(context, users, admin.MemberID, now);
            await SeedFavoritesAsync(context, users, restaurants, folders, admin.MemberID, now);
            var reports = await SeedReportsAsync(
                context, users, approvalOwners, restaurants, reviews, images, admin.MemberID, now);
            await RecalculateReviewReportCountsAsync(context);
            await RecalculateRestaurantStatsAsync(context);
            await SeedNotificationsAsync(context, users, levels, reports, admin.MemberID, now);
            var frames = await SeedAvatarFramesAsync(context, images, admin.MemberID, now);
            await SeedMemberFramesAndPointsAsync(context, users, frames, admin.MemberID, now);

            await context.SaveChangesAsync();
            await ValidateAsync(context, environment, adminPassword);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        logger.LogInformation(
            "Formal presentation data ready; Database={Database}; RecordCount={RecordCount}; PhysicalImages={PhysicalImages}; AdminEmail={AdminEmail}",
            databaseName,
            ExpectedRecordCount,
            ExpectedPhysicalImageCount,
            AdminEmail);
    }

    public static async Task RefreshMemberAvatarsAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MidProject.Data.FormalDemoDataSeeder");
        var databaseName = EnsureRequiredDatabase(context, "Formal member-avatar refresh");

        var users = await context.Members
            .Where(member => member.Role == "User")
            .OrderBy(member => member.MemberID)
            .ToListAsync();
        var avatarImages = await context.Images
            .Where(image => image.ImageType == "MemberAvatar")
            .OrderBy(image => image.ImageID)
            .ToListAsync();

        await AssignMemberAvatarsAsync(context, users, avatarImages);

        logger.LogInformation(
            "Formal member avatars refreshed; Database={Database}; CustomAvatars={CustomAvatars}; DefaultAvatars={DefaultAvatars}",
            databaseName,
            ExpectedCustomMemberAvatarCount,
            await context.Members.CountAsync(member => member.AvatarImageID == null));
    }

    public static async Task RefreshDashboardDistributionAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ITaipeiClock>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MidProject.Data.FormalDemoDataSeeder");
        var databaseName = EnsureRequiredDatabase(
            context,
            "Formal dashboard-distribution refresh");
        var now = clock.GetNow();

        var users = await context.Members
            .Where(member => member.Role == "User")
            .OrderBy(member => member.MemberID)
            .ToListAsync();
        var restaurants = await context.Restaurants
            .OrderBy(restaurant => restaurant.RestaurantID)
            .ToListAsync();
        var reviews = await context.Reviews
            .OrderBy(review => review.ReviewID)
            .ToListAsync();
        var favorites = await context.Favorites
            .OrderBy(favorite => favorite.FavoriteID)
            .ToListAsync();
        var reports = await context.Reports
            .OrderBy(report => report.ReportID)
            .ToListAsync();

        for (var index = 0; index < users.Count; index++)
        {
            users[index].CreatedAt = GetWeightedCreatedAt(
                now,
                index,
                MemberRecentMonthlyCounts);
        }

        for (var index = 0; index < restaurants.Count; index++)
        {
            restaurants[index].CreatedAt = GetWeightedCreatedAt(
                now,
                index,
                RestaurantRecentMonthlyCounts);
        }

        for (var index = 0; index < reviews.Count; index++)
        {
            var restaurantIndex = GetWeightedRestaurantIndex(
                index,
                restaurants.Count,
                PopularRestaurantReviewCounts);
            reviews[index].RestaurantID = restaurants[restaurantIndex].RestaurantID;
            reviews[index].Rating = GetReviewRating(index, restaurantIndex);
            reviews[index].CreatedAt = GetWeightedCreatedAt(
                now,
                index,
                ReviewRecentMonthlyCounts);
        }

        for (var index = 0; index < favorites.Count; index++)
        {
            var restaurantIndex = GetWeightedRestaurantIndex(
                index,
                restaurants.Count,
                PopularRestaurantFavoriteCounts);
            favorites[index].RestaurantID = restaurants[restaurantIndex].RestaurantID;
        }

        for (var index = 0; index < reports.Count; index++)
        {
            var status = index < 100
                ? "Approved"
                : index < 170
                    ? "Rejected"
                    : "Pending";
            var createdAt = GetWeightedCreatedAt(
                now,
                index,
                ReportRecentMonthlyCounts);
            reports[index].Status = status;
            reports[index].CreatedAt = createdAt;

            if (status == "Pending")
            {
                reports[index].HandledAt = null;
                reports[index].HandledByMemberID = null;
                reports[index].AdminNote = null;
            }
            else
            {
                var handledAt = createdAt.AddHours(index % 72 + 1);
                reports[index].HandledAt = handledAt <= now ? handledAt : now;
            }
        }

        await context.SaveChangesAsync();
        await RecalculateRestaurantStatsAsync(context);

        var reportStatusCounts = await context.Reports
            .GroupBy(report => report.Status)
            .ToDictionaryAsync(group => group.Key, group => group.Count());
        logger.LogInformation(
            "Formal dashboard distribution refreshed; Database={Database}; Members={Members}; Restaurants={Restaurants}; Reviews={Reviews}; Favorites={Favorites}; ApprovedReports={ApprovedReports}; RejectedReports={RejectedReports}; PendingReports={PendingReports}",
            databaseName,
            users.Count,
            restaurants.Count,
            reviews.Count,
            favorites.Count,
            reportStatusCounts.GetValueOrDefault("Approved"),
            reportStatusCounts.GetValueOrDefault("Rejected"),
            reportStatusCounts.GetValueOrDefault("Pending"));
    }

    private static string EnsureRequiredDatabase(AppDbContext context, string operation)
    {
        var databaseName = context.Database.GetDbConnection().Database;
        if (!string.Equals(databaseName, RequiredDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{operation} refused: expected database '{RequiredDatabaseName}', " +
                $"but the configured database is '{databaseName}'.");
        }

        return databaseName;
    }

    private static DateTime GetWeightedCreatedAt(
        DateTime now,
        int itemIndex,
        IReadOnlyList<int> recentMonthlyCounts)
    {
        var currentMonthStart = new DateTime(now.Year, now.Month, 1);
        var cursor = 0;
        for (var monthIndex = 0; monthIndex < recentMonthlyCounts.Count; monthIndex++)
        {
            var monthCount = recentMonthlyCounts[monthIndex];
            if (itemIndex < cursor + monthCount)
            {
                var targetMonth = currentMonthStart.AddMonths(
                    monthIndex - recentMonthlyCounts.Count + 1);
                var daysInMonth = DateTime.DaysInMonth(
                    targetMonth.Year,
                    targetMonth.Month);
                var dayIndex = (itemIndex * 7 + monthIndex * 3) % daysInMonth;
                var createdAt = targetMonth
                    .AddDays(dayIndex)
                    .AddMinutes((itemIndex * 37 + monthIndex * 53) % 1440);
                return createdAt <= now
                    ? createdAt
                    : now.AddMinutes(-(itemIndex % 600 + 1));
            }

            cursor += monthCount;
        }

        var olderIndex = itemIndex - cursor;
        return currentMonthStart
            .AddMonths(-6)
            .AddDays(-((olderIndex * 11) % 540 + 1))
            .AddMinutes((olderIndex * 29) % 1440);
    }

    private static int GetWeightedRestaurantIndex(
        int itemIndex,
        int restaurantCount,
        IReadOnlyList<int> popularCounts)
    {
        if (PopularRestaurantIndexes.Any(index => index >= restaurantCount))
        {
            throw new InvalidOperationException(
                "Formal dashboard distribution does not contain enough restaurants.");
        }

        var cursor = 0;
        for (var index = 0; index < popularCounts.Count; index++)
        {
            if (itemIndex < cursor + popularCounts[index])
            {
                return PopularRestaurantIndexes[index];
            }

            cursor += popularCounts[index];
        }

        const int generalRestaurantStartIndex = 40;
        return generalRestaurantStartIndex +
            ((itemIndex - cursor) * 37 %
             (restaurantCount - generalRestaurantStartIndex));
    }

    private static int GetReviewRating(int reviewIndex, int restaurantIndex)
    {
        var popularIndex = Array.IndexOf(
            PopularRestaurantIndexes,
            restaurantIndex);
        if (popularIndex < 0)
        {
            return reviewIndex % 5 + 1;
        }

        var baseRating = PopularRestaurantBaseRatings[popularIndex];
        return reviewIndex % 6 == 0
            ? Math.Max(1, baseRating - 1)
            : baseRating;
    }

    // Table identifiers cannot be SQL parameters. Both lists are private compile-time allowlists,
    // never request/configuration input, so interpolation here cannot be influenced externally.
#pragma warning disable EF1002
    private static async Task ClearApplicationDataAsync(AppDbContext context)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            foreach (var table in ApplicationTables)
            {
                await context.Database.ExecuteSqlRawAsync(
                    $"ALTER TABLE [dbo].[{table}] NOCHECK CONSTRAINT ALL;");
            }

            foreach (var table in ApplicationTables)
            {
                await context.Database.ExecuteSqlRawAsync($"DELETE FROM [dbo].[{table}];");
            }

            foreach (var table in IdentityTables)
            {
                await context.Database.ExecuteSqlRawAsync(
                    $"DBCC CHECKIDENT ('[dbo].[{table}]', RESEED, 0) WITH NO_INFOMSGS;");
            }

            foreach (var table in ApplicationTables.Reverse())
            {
                await context.Database.ExecuteSqlRawAsync(
                    $"ALTER TABLE [dbo].[{table}] WITH CHECK CHECK CONSTRAINT ALL;");
            }

            await transaction.CommitAsync();
            context.ChangeTracker.Clear();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
#pragma warning restore EF1002

    private static async Task AssertDatabaseEmptyAsync(AppDbContext context)
    {
        var counts = await GetCountsAsync(context);
        var remaining = counts.Where(item => item.Value != 0).ToArray();
        if (remaining.Length != 0)
        {
            throw new InvalidOperationException(
                "Database reset verification failed: " +
                string.Join(", ", remaining.Select(item => $"{item.Key}={item.Value}")));
        }
    }

    private static void ReplaceRuntimeImages(
        IWebHostEnvironment environment,
        IReadOnlyList<string> sourceFiles)
    {
        var uploadRoot = Path.GetFullPath(Path.Combine(environment.WebRootPath, "uploads"));
        var expectedRoot = Path.GetFullPath(environment.WebRootPath)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!uploadRoot.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFileName(uploadRoot), "uploads", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved upload directory is outside the expected web root.");
        }

        Directory.CreateDirectory(uploadRoot);
        foreach (var file in Directory.EnumerateFiles(uploadRoot, "*", SearchOption.AllDirectories))
        {
            if (!string.Equals(Path.GetFileName(file), ".gitkeep", StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(file);
            }
        }

        var groups = new[]
        {
            (Directory: "RestaurantCover", Prefix: "formal-cover", Count: 50),
            (Directory: "RestaurantEnvironment", Prefix: "formal-environment", Count: 30),
            (Directory: "ReviewImage", Prefix: "formal-review", Count: 20),
            (Directory: "MemberAvatar", Prefix: "formal-avatar", Count: 10),
            (Directory: "AvatarFrame", Prefix: "formal-frame", Count: 10)
        };

        var sourceIndex = 0;
        foreach (var group in groups)
        {
            var destinationDirectory = Path.Combine(uploadRoot, group.Directory);
            Directory.CreateDirectory(destinationDirectory);
            for (var index = 1; index <= group.Count; index++)
            {
                File.Copy(
                    sourceFiles[sourceIndex++],
                    Path.Combine(destinationDirectory, $"{group.Prefix}-{index:D3}.jpg"),
                    overwrite: true);
            }
        }
    }

    private static async Task<List<UserLevel>> SeedLevelsAsync(AppDbContext context)
    {
        var levels = new List<UserLevel>
        {
            new() { LevelName = LevelNames[0], MinExp = 0, Rewards = "基本會員權益" },
            new() { LevelName = LevelNames[1], MinExp = 500, Rewards = "評論徽章" },
            new() { LevelName = LevelNames[2], MinExp = 1300, Rewards = "達人標章" },
            new() { LevelName = LevelNames[3], MinExp = 3400, Rewards = "專屬活動邀請" },
            new() { LevelName = LevelNames[4], MinExp = 8800, Rewards = "VIP 標章" },
            new() { LevelName = LevelNames[5], MinExp = 23000, Rewards = "尊爵頭銜", IsDeleted = true }
        };
        context.UserLevels.AddRange(levels);
        await context.SaveChangesAsync();
        return levels;
    }

    private static async Task<(Member Admin, List<Member> Users, List<int> ApprovalOwners)>
        SeedMembersAsync(
            AppDbContext context,
            IReadOnlyList<UserLevel> levels,
            string adminPassword,
            DateTime now)
    {
        var admin = new Member
        {
            UserName = "Admin",
            NickName = "Admin",
            Email = AdminEmail,
            PasswordHash = PasswordHashService.HashPassword(adminPassword),
            Role = "Admin",
            Status = "Normal",
            IsActive = true,
            LevelID = levels[4].LevelID,
            Experience = 12_000,
            Points = 5_000,
            Phone = "02-2345-6789",
            CreatedAt = now.AddYears(-2),
            UpdatedAt = now
        };
        context.Members.Add(admin);
        await context.SaveChangesAsync();

        var inaccessiblePasswordHash = PasswordHashService.HashPassword(
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        var users = new List<Member>(799);
        for (var index = 1; index <= 799; index++)
        {
            var createdAt = GetWeightedCreatedAt(
                now,
                index - 1,
                MemberRecentMonthlyCounts);
            var levelIndex = index % levels.Count;
            var member = new Member
            {
                UserName = $"foodie{index:D4}",
                NickName = $"{Surnames[index % Surnames.Length]}{GivenNames[index % GivenNames.Length]}",
                Email = $"foodie{index:D4}@example.com",
                PasswordHash = inaccessiblePasswordHash,
                Phone = index % 4 == 0 ? null : $"09{index % 100_000_000:D8}",
                Role = "User",
                Status = "Normal",
                IsActive = true,
                LevelID = levels[levelIndex].LevelID,
                Experience = Math.Max(levels[levelIndex].MinExp, (index * 47) % 28_000),
                Points = 3_000,
                Birthday = new DateOnly(1975 + index % 30, 1 + index % 12, 1 + index % 27),
                CreatedAt = createdAt,
                UpdatedAt = now.AddDays(-(index % 30))
            };

            if (index <= 10)
            {
                member.Status = "Warning";
                member.WarningCount = 1;
                member.AdminNote = "已有一件成立檢舉，請留意平台規範。";
            }
            else if (index <= 20)
            {
                member.Status = "Muted";
                member.WarningCount = 2;
                member.PenaltyEndAt = now.AddDays(7);
                member.AdminNote = "累積兩件成立檢舉，暫停互動七日。";
            }
            else if (index <= 30)
            {
                member.Status = "Muted";
                member.WarningCount = 3;
                member.PenaltyEndAt = now.AddMonths(1);
                member.AdminNote = "累積三件成立檢舉，暫停互動一個月。";
            }
            else if (index <= 40)
            {
                member.Status = "Suspended";
                member.WarningCount = 4;
                member.IsDeleted = true;
                member.DeletedAt = now.AddDays(-3);
                member.DeletedBy = admin.MemberID;
                member.AdminNote = "累積四件成立檢舉，帳號已停權。";
            }
            else if (index % 97 == 0)
            {
                member.Status = "Deleted";
                member.IsDeleted = true;
                member.DeletedAt = now.AddDays(-(index % 90 + 1));
                member.DeletedBy = admin.MemberID;
                member.AdminNote = "會員申請註銷帳號。";
            }
            else if (index % 89 == 0)
            {
                member.IsActive = false;
                member.AdminNote = "帳號暫停使用。";
            }
            else if (index % 83 == 0)
            {
                member.IsLocked = true;
                member.FailedLoginCount = 3;
                member.AdminNote = "管理員手動鎖定。";
            }
            else if (index % 79 == 0)
            {
                member.IsLocked = true;
                member.FailedLoginCount = 3;
                member.LoginLockoutEndAt = now.AddMinutes(15);
            }
            else if (index % 73 == 0)
            {
                member.IsLocked = true;
                member.FailedLoginCount = 3;
                member.LoginLockoutEndAt = now.AddMinutes(-5);
            }

            users.Add(member);
        }

        context.Members.AddRange(users);
        await context.SaveChangesAsync();

        var approvalOwners = new List<int>(100);
        for (var ownerIndex = 0; ownerIndex < 40; ownerIndex++)
        {
            var count = ownerIndex switch
            {
                < 10 => 1,
                < 20 => 2,
                < 30 => 3,
                _ => 4
            };
            for (var occurrence = 0; occurrence < count; occurrence++)
            {
                approvalOwners.Add(users[ownerIndex].MemberID);
            }
        }

        return (admin, users, approvalOwners);
    }

    private static async Task<List<Tag>> SeedTagsAsync(
        AppDbContext context,
        int adminId,
        DateTime now)
    {
        var tags = TagNames.Select((name, index) => new Tag
        {
            TagName = name,
            SortOrder = index,
            IsDeleted = index >= 35,
            DeletedAt = index >= 35 ? now.AddDays(-(index - 30)) : null,
            DeletedBy = index >= 35 ? adminId : null
        }).ToList();
        context.Tags.AddRange(tags);
        await context.SaveChangesAsync();
        return tags;
    }

    private static async Task<List<Restaurant>> SeedRestaurantsAsync(
        AppDbContext context,
        IReadOnlyList<Member> users,
        IReadOnlyList<int> approvalOwners,
        int adminId,
        DateTime now)
    {
        var approvedRestaurantOwners = approvalOwners
            .Where((_, index) => index % 3 == 0)
            .ToArray();
        var restaurants = new List<Restaurant>(400);
        for (var index = 0; index < 400; index++)
        {
            var location = Locations[index % Locations.Length];
            var district = location.Districts[index % location.Districts.Length];
            var isApprovedTarget = index < approvedRestaurantOwners.Length;
            var isDeleted = isApprovedTarget || (!isApprovedTarget && index % 17 == 0);
            var ownerId = isApprovedTarget
                ? approvedRestaurantOwners[index]
                : users[(index * 7 + 40) % users.Count].MemberID;
            var createdAt = GetWeightedCreatedAt(
                now,
                index,
                RestaurantRecentMonthlyCounts);

            restaurants.Add(new Restaurant
            {
                Name = $"{RestaurantPrefixes[index % RestaurantPrefixes.Length]}{RestaurantSuffixes[(index / 10) % RestaurantSuffixes.Length]} {index + 1:D3}",
                City = location.City,
                District = district,
                DetailedAddress = $"{district}示範路 {10 + index % 190} 號{(index % 5) + 1}樓",
                Phone = $"0{2 + index % 7}-{2000 + index % 7000:D4}-{1000 + index % 9000:D4}",
                Note = index % 9 == 0 ? "假日建議提前訂位，餐點依當日食材供應為準。" : "提供內用與外帶服務。",
                Latitude = 22.60m + (index % 300) * 0.01m,
                Longitude = 120.20m + (index % 220) * 0.01m,
                MemberID = ownerId,
                CreatedAt = createdAt,
                UpdatedAt = now.AddDays(-(index % 30)),
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? now.AddDays(-(index % 20 + 1)) : null,
                DeletedBy = isDeleted ? adminId : null,
                DeleteReason = isApprovedTarget
                    ? "檢舉成立，餐廳內容已下架。"
                    : isDeleted ? "店家歇業，保留歷史資料。" : null
            });
        }

        context.Restaurants.AddRange(restaurants);
        await context.SaveChangesAsync();
        return restaurants;
    }

    private static async Task SeedBusinessHoursAsync(
        AppDbContext context,
        IReadOnlyList<Restaurant> restaurants)
    {
        var hours = new List<BusinessHour>(2_800);
        foreach (var (restaurant, restaurantIndex) in restaurants.Select((item, index) => (item, index)))
        {
            for (var day = 1; day <= 7; day++)
            {
                var closed = (restaurantIndex + day) % 11 == 0;
                hours.Add(new BusinessHour
                {
                    RestaurantID = restaurant.RestaurantID,
                    DayOfWeek = day,
                    OpenTime = closed ? TimeOnly.MinValue : new TimeOnly(10 + restaurantIndex % 2, 0),
                    CloseTime = closed ? TimeOnly.MinValue : new TimeOnly(20 + restaurantIndex % 3, 0),
                    IsClosed = closed
                });
            }
        }

        context.BusinessHours.AddRange(hours);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task SeedRestaurantTagsAsync(
        AppDbContext context,
        IReadOnlyList<Restaurant> restaurants,
        IReadOnlyList<Tag> tags)
    {
        var links = new List<RestaurantTag>(1_200);
        foreach (var (restaurant, index) in restaurants.Select((item, index) => (item, index)))
        {
            for (var offset = 0; offset < 3; offset++)
            {
                links.Add(new RestaurantTag
                {
                    RestaurantID = restaurant.RestaurantID,
                    TagID = tags[(index * 3 + offset) % tags.Count].TagID
                });
            }
        }

        context.RestaurantTags.AddRange(links);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task<List<Image>> SeedImagesAsync(
        AppDbContext context,
        IReadOnlyList<Member> users,
        IReadOnlyList<int> approvalOwners,
        int adminId,
        DateTime now)
    {
        var approvedImageOwners = approvalOwners
            .Where((_, index) => index % 3 == 2)
            .ToArray();
        var images = new List<Image>(620);
        for (var index = 0; index < 620; index++)
        {
            string imageType;
            string imageUrl;
            int sortOrder;
            if (index < 400)
            {
                imageType = "RestaurantCover";
                imageUrl = $"/uploads/RestaurantCover/formal-cover-{index % 50 + 1:D3}.jpg";
                sortOrder = 0;
            }
            else if (index < 450)
            {
                imageType = "RestaurantEnvironment";
                imageUrl = $"/uploads/RestaurantEnvironment/formal-environment-{(index - 400) % 30 + 1:D3}.jpg";
                sortOrder = index - 399;
            }
            else if (index < 550)
            {
                imageType = "ReviewImage";
                imageUrl = $"/uploads/ReviewImage/formal-review-{(index - 450) % 20 + 1:D3}.jpg";
                sortOrder = (index - 450) % 5;
            }
            else if (index < 590)
            {
                imageType = "MemberAvatar";
                imageUrl = $"/uploads/MemberAvatar/formal-avatar-{(index - 550) % 10 + 1:D3}.jpg";
                sortOrder = 0;
            }
            else
            {
                imageType = "AvatarFrame";
                imageUrl = $"/uploads/AvatarFrame/formal-frame-{(index - 590) % 10 + 1:D3}.jpg";
                sortOrder = index - 590;
            }

            var isApprovedTarget = index < approvedImageOwners.Length;
            var isDeleted = isApprovedTarget || (!isApprovedTarget && index % 41 == 0);
            images.Add(new Image
            {
                UploadedByMemberID = isApprovedTarget
                    ? approvedImageOwners[index]
                    : users[(index * 11 + 50) % users.Count].MemberID,
                ImageURL = imageUrl,
                ImageType = imageType,
                SortOrder = sortOrder,
                UploadedAt = now.AddDays(-(index % 650 + 1)),
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? now.AddDays(-(index % 20 + 1)) : null,
                DeletedBy = isDeleted ? adminId : null
            });
        }

        context.Images.AddRange(images);
        await context.SaveChangesAsync();

        await AssignMemberAvatarsAsync(
            context,
            users,
            images.Where(image => image.ImageType == "MemberAvatar").ToArray());
        return images;
    }

    private static async Task AssignMemberAvatarsAsync(
        AppDbContext context,
        IReadOnlyList<Member> users,
        IReadOnlyList<Image> avatarImages)
    {
        var availableAvatarImages = avatarImages
            .Where(image => !image.IsDeleted)
            .OrderBy(image => image.ImageID)
            .ToArray();
        if (availableAvatarImages.Length == 0)
        {
            throw new InvalidOperationException(
                "Formal member-avatar assignment requires at least one active MemberAvatar image.");
        }

        foreach (var user in users)
        {
            user.AvatarImageID = null;
        }

        var membersWithCustomAvatars = users
            .Skip(40)
            .Take(ExpectedCustomMemberAvatarCount)
            .ToArray();
        if (membersWithCustomAvatars.Length != ExpectedCustomMemberAvatarCount)
        {
            throw new InvalidOperationException(
                $"Formal member-avatar assignment expected {ExpectedCustomMemberAvatarCount} eligible members, " +
                $"found {membersWithCustomAvatars.Length}.");
        }

        for (var index = 0; index < membersWithCustomAvatars.Length; index++)
        {
            membersWithCustomAvatars[index].AvatarImageID =
                availableAvatarImages[index % availableAvatarImages.Length].ImageID;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedRestaurantImagesAsync(
        AppDbContext context,
        IReadOnlyList<Restaurant> restaurants,
        IReadOnlyList<Image> images)
    {
        var links = new List<RestaurantImage>(450);
        for (var index = 0; index < 400; index++)
        {
            links.Add(new RestaurantImage
            {
                RestaurantID = restaurants[index].RestaurantID,
                ImageID = images[index].ImageID
            });
        }
        for (var index = 0; index < 19; index++)
        {
            links.Add(new RestaurantImage
            {
                RestaurantID = restaurants[0].RestaurantID,
                ImageID = images[400 + index].ImageID
            });
        }
        for (var index = 19; index < 50; index++)
        {
            links.Add(new RestaurantImage
            {
                RestaurantID = restaurants[index - 18].RestaurantID,
                ImageID = images[400 + index].ImageID
            });
        }

        context.RestaurantImages.AddRange(links);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task<List<Review>> SeedReviewsAsync(
        AppDbContext context,
        IReadOnlyList<Member> users,
        IReadOnlyList<int> approvalOwners,
        IReadOnlyList<Restaurant> restaurants,
        int adminId,
        DateTime now)
    {
        var approvedReviewOwners = approvalOwners
            .Where((_, index) => index % 3 == 1)
            .ToArray();
        var reviews = new List<Review>(900);
        for (var index = 0; index < 900; index++)
        {
            var isApprovedTarget = index < approvedReviewOwners.Length;
            var isDeleted = isApprovedTarget || (!isApprovedTarget && index % 29 == 0);
            var restaurantIndex = GetWeightedRestaurantIndex(
                index,
                restaurants.Count,
                PopularRestaurantReviewCounts);
            reviews.Add(new Review
            {
                MemberID = isApprovedTarget
                    ? approvedReviewOwners[index]
                    : users[(index * 13 + 60) % users.Count].MemberID,
                RestaurantID = restaurants[restaurantIndex].RestaurantID,
                Rating = GetReviewRating(index, restaurantIndex),
                Content = $"{ReviewTexts[index % ReviewTexts.Length]}（用餐紀錄 {index + 1:D4}）",
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? now.AddDays(-(index % 20 + 1)) : null,
                DeletedBy = isDeleted ? adminId : null,
                Status = index % 9 == 0 ? "PendingReview" : "Active",
                CreatedAt = GetWeightedCreatedAt(
                    now,
                    index,
                    ReviewRecentMonthlyCounts),
                UpdatedAt = index % 4 == 0 ? now.AddDays(-(index % 60)) : null
            });
        }

        context.Reviews.AddRange(reviews);
        await context.SaveChangesAsync();
        return reviews;
    }

    private static async Task SeedReviewImagesAsync(
        AppDbContext context,
        IReadOnlyList<Review> reviews,
        IReadOnlyList<Image> images)
    {
        var links = Enumerable.Range(0, 100).Select(index => new ReviewImage
        {
            ReviewID = reviews[index].ReviewID,
            ImageID = images[450 + index].ImageID
        });
        context.ReviewImages.AddRange(links);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task<List<FavoriteFolder>> SeedFavoriteFoldersAsync(
        AppDbContext context,
        IReadOnlyList<Member> users,
        int adminId,
        DateTime now)
    {
        var folderNames = new[] { "近期想吃", "朋友聚餐", "約會清單", "週末探索", "外帶收藏" };
        var folders = Enumerable.Range(0, 300).Select(index =>
        {
            var deleted = index % 23 == 0;
            return new FavoriteFolder
            {
                MemberID = users[index].MemberID,
                FolderName = $"{folderNames[index % folderNames.Length]} {index + 1:D3}",
                CreatedAt = now.AddDays(-(index % 500 + 1)),
                UpdatedAt = now.AddDays(-(index % 45)),
                IsDeleted = deleted,
                DeletedAt = deleted ? now.AddDays(-(index % 30 + 1)) : null,
                DeletedBy = deleted ? adminId : null
            };
        }).ToList();
        context.FavoriteFolders.AddRange(folders);
        await context.SaveChangesAsync();
        return folders;
    }

    private static async Task SeedFavoritesAsync(
        AppDbContext context,
        IReadOnlyList<Member> users,
        IReadOnlyList<Restaurant> restaurants,
        IReadOnlyList<FavoriteFolder> folders,
        int adminId,
        DateTime now)
    {
        var favorites = new List<Favorite>(600);
        for (var index = 0; index < folders.Count; index++)
        {
            for (var offset = 0; offset < 2; offset++)
            {
                var favoriteIndex = index * 2 + offset;
                var deleted = favoriteIndex % 31 == 0;
                var restaurantIndex = GetWeightedRestaurantIndex(
                    favoriteIndex,
                    restaurants.Count,
                    PopularRestaurantFavoriteCounts);
                favorites.Add(new Favorite
                {
                    MemberID = folders[index].MemberID,
                    RestaurantID = restaurants[restaurantIndex].RestaurantID,
                    FavoriteFolderID = folders[index].FavoriteFolderID,
                    CreatedAt = now.AddDays(-(favoriteIndex % 400 + 1)),
                    UpdatedAt = now.AddDays(-(index % 25)),
                    IsDeleted = deleted,
                    DeletedAt = deleted ? now.AddDays(-(index % 20 + 1)) : null,
                    DeletedBy = deleted ? adminId : null
                });
            }
        }

        context.Favorites.AddRange(favorites);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task<List<Report>> SeedReportsAsync(
        AppDbContext context,
        IReadOnlyList<Member> users,
        IReadOnlyList<int> approvalOwners,
        IReadOnlyList<Restaurant> restaurants,
        IReadOnlyList<Review> reviews,
        IReadOnlyList<Image> images,
        int adminId,
        DateTime now)
    {
        var approvedRestaurants = restaurants
            .Take(approvalOwners.Where((_, index) => index % 3 == 0).Count())
            .ToArray();
        var approvedReviews = reviews
            .Take(approvalOwners.Where((_, index) => index % 3 == 1).Count())
            .ToArray();
        var approvedImages = images
            .Take(approvalOwners.Where((_, index) => index % 3 == 2).Count())
            .ToArray();
        var approvedRestaurantIndex = 0;
        var approvedReviewIndex = 0;
        var approvedImageIndex = 0;
        var reports = new List<Report>(300);

        for (var index = 0; index < 300; index++)
        {
            var status = index < 100 ? "Approved" : index < 170 ? "Rejected" : "Pending";
            var targetType = index % 3;
            int ownerId;
            int? restaurantId = null;
            int? reviewId = null;
            int? imageId = null;

            if (status == "Approved")
            {
                ownerId = approvalOwners[index];
                if (targetType == 0)
                {
                    var target = approvedRestaurants[approvedRestaurantIndex++];
                    restaurantId = target.RestaurantID;
                }
                else if (targetType == 1)
                {
                    var target = approvedReviews[approvedReviewIndex++];
                    reviewId = target.ReviewID;
                }
                else
                {
                    var target = approvedImages[approvedImageIndex++];
                    imageId = target.ImageID;
                }
            }
            else if (targetType == 0)
            {
                var target = restaurants[100 + index % 250];
                restaurantId = target.RestaurantID;
                ownerId = target.MemberID;
            }
            else if (targetType == 1)
            {
                var target = reviews[100 + index % 700];
                reviewId = target.ReviewID;
                ownerId = target.MemberID;
            }
            else
            {
                var target = images[100 + index % 450];
                imageId = target.ImageID;
                ownerId = target.UploadedByMemberID;
            }

            var reporter = users[(index * 19 + 300) % users.Count];
            if (reporter.MemberID == ownerId)
            {
                reporter = users[(index * 19 + 301) % users.Count];
            }
            var handled = status != "Pending";
            var deleted = index % 37 == 0;
            var createdAt = GetWeightedCreatedAt(
                now,
                index,
                ReportRecentMonthlyCounts);
            reports.Add(new Report
            {
                ReporterMemberID = reporter.MemberID,
                ReportedMemberID = ownerId == adminId ? null : ownerId,
                RestaurantID = restaurantId,
                ReviewID = reviewId,
                ImageID = imageId,
                Reason = $"{Categories[index % Categories.Length]}相關內容需要平台協助確認，案件編號 {index + 1:D4}。",
                Category = Categories[index % Categories.Length],
                Status = status,
                CreatedAt = createdAt,
                HandledAt = handled ? createdAt.AddDays(index % 8 + 1) : null,
                HandledByMemberID = handled ? adminId : null,
                AdminNote = handled ? (status == "Approved" ? "查核後確認違規。" : "查核後未發現違規。") : null,
                IsDeleted = deleted,
                DeletedAt = deleted ? now.AddDays(-(index % 10 + 1)) : null,
                DeletedBy = deleted ? adminId : null
            });
        }

        context.Reports.AddRange(reports);
        await context.SaveChangesAsync();
        return reports;
    }

    private static async Task RecalculateReviewReportCountsAsync(AppDbContext context)
    {
        var counts = await context.Reports
            .Where(report => report.ReviewID != null && !report.IsDeleted)
            .GroupBy(report => report.ReviewID!.Value)
            .ToDictionaryAsync(group => group.Key, group => group.Count());
        var reviews = await context.Reviews.ToListAsync();
        foreach (var review in reviews)
        {
            review.ReportCount = counts.GetValueOrDefault(review.ReviewID);
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task RecalculateRestaurantStatsAsync(AppDbContext context)
    {
        var stats = await context.Reviews
            .Where(review => !review.IsDeleted && review.Status == "Active")
            .GroupBy(review => review.RestaurantID)
            .Select(group => new
            {
                RestaurantID = group.Key,
                Count = group.Count(),
                Average = group.Average(review => review.Rating)
            })
            .ToDictionaryAsync(item => item.RestaurantID);
        var restaurants = await context.Restaurants.ToListAsync();
        foreach (var restaurant in restaurants)
        {
            if (stats.TryGetValue(restaurant.RestaurantID, out var stat))
            {
                restaurant.ReviewCount = stat.Count;
                restaurant.AverageRating = Math.Round((decimal)stat.Average, 2);
            }
            else
            {
                restaurant.ReviewCount = 0;
                restaurant.AverageRating = 0;
            }
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task SeedNotificationsAsync(
        AppDbContext context,
        IReadOnlyList<Member> users,
        IReadOnlyList<UserLevel> levels,
        IReadOnlyList<Report> reports,
        int adminId,
        DateTime now)
    {
        var handledReports = reports.Where(report => report.Status != "Pending").Take(60).ToArray();
        var notifications = new List<Notification>(300);
        for (var index = 0; index < 300; index++)
        {
            var isSourceNotification = index < handledReports.Length;
            var isPersonal = isSourceNotification || index % 2 == 0;
            var deleted = !isSourceNotification && index % 23 == 0;
            var sent = !deleted && index % 3 == 0;
            var scheduledAt = sent
                ? now.AddDays(-(index % 90 + 1))
                : index % 2 == 0
                    ? now.AddDays(-(index % 30 + 1))
                    : now.AddDays(index % 30 + 1);
            var notification = new Notification
            {
                NotificationType = isPersonal ? "Personal" : "Condition",
                MemberID = isPersonal ? users[(index * 7 + 100) % users.Count].MemberID : null,
                Title = isSourceNotification
                    ? $"檢舉案件 #{handledReports[index].ReportID} 處理結果"
                    : $"美食地圖營運通知 {index + 1:D3}",
                Content = isSourceNotification
                    ? $"您提交或涉及的檢舉案件已完成審核，結果為{(handledReports[index].Status == "Approved" ? "檢舉成立" : "駁回檢舉")}。"
                    : "平台已更新餐廳、收藏與會員活動資訊，請登入查看最新內容。",
                ScheduledAt = scheduledAt,
                SentAt = sent ? scheduledAt.AddMinutes(2) : null,
                IsSent = sent,
                CreatedAt = scheduledAt.AddDays(-1),
                CreatedBy = adminId,
                IsDeleted = deleted,
                DeletedAt = deleted ? now.AddDays(-1) : null,
                DeletedBy = deleted ? adminId : null,
                SourceReportID = isSourceNotification ? handledReports[index].ReportID : null,
                SourceReportOutcome = isSourceNotification ? handledReports[index].Status : null
            };

            if (!isPersonal)
            {
                switch (index % 4)
                {
                    case 0:
                        notification.TargetRole = "User";
                        break;
                    case 1:
                        notification.TargetStatus = "Normal";
                        break;
                    case 2:
                        notification.TargetLevelID = levels[index % levels.Count].LevelID;
                        break;
                    default:
                        notification.TargetRole = "Admin";
                        break;
                }
            }
            notifications.Add(notification);
        }

        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task<List<AvatarFrame>> SeedAvatarFramesAsync(
        AppDbContext context,
        IReadOnlyList<Image> images,
        int adminId,
        DateTime now)
    {
        var frameNames = new[] { "晨光", "星夜", "海洋", "森林", "櫻花", "金曜", "雲朵", "琥珀", "霓虹", "典藏" };
        var frames = Enumerable.Range(0, 30).Select(index =>
        {
            var deleted = index % 11 == 0;
            return new AvatarFrame
            {
                Name = $"{frameNames[index % frameNames.Length]}外框 {index + 1:D2}",
                Description = "點數商城限定頭像外框，可於會員個人頁面裝備。",
                Rarity = index % 3 == 0 ? "Limited" : index % 3 == 1 ? "Rare" : "Common",
                PointsPrice = 100 + index * 50,
                ImageID = images[590 + index].ImageID,
                SortOrder = index,
                IsActive = !deleted && index % 7 != 0,
                CreatedAt = now.AddDays(-(index * 8 + 1)),
                UpdatedAt = now.AddDays(-(index % 10)),
                IsDeleted = deleted,
                DeletedAt = deleted ? now.AddDays(-(index % 15 + 1)) : null,
                DeletedBy = deleted ? adminId : null
            };
        }).ToList();
        context.AvatarFrames.AddRange(frames);
        await context.SaveChangesAsync();
        return frames;
    }

    private static async Task SeedMemberFramesAndPointsAsync(
        AppDbContext context,
        IReadOnlyList<Member> users,
        IReadOnlyList<AvatarFrame> frames,
        int adminId,
        DateTime now)
    {
        var userIds = users.Select(member => member.MemberID).ToArray();
        var persistedUsers = await context.Members
            .Where(member => userIds.Contains(member.MemberID))
            .OrderBy(member => member.MemberID)
            .ToListAsync();
        var availableFrames = frames.Where(frame => !frame.IsDeleted).ToArray();
        var ownerships = new List<MemberAvatarFrame>(250);
        var transactions = new List<PointsTransaction>(904);
        var balances = persistedUsers.ToDictionary(member => member.MemberID, _ => 3_000);

        for (var index = 0; index < 250; index++)
        {
            var member = persistedUsers[index + 100];
            var frame = availableFrames[index % availableFrames.Length];
            var redeemedAt = now.AddDays(-(index % 180 + 1));
            ownerships.Add(new MemberAvatarFrame
            {
                MemberID = member.MemberID,
                FrameID = frame.FrameID,
                RedeemedAt = redeemedAt
            });
            balances[member.MemberID] -= frame.PointsPrice;
            transactions.Add(new PointsTransaction
            {
                MemberID = member.MemberID,
                Amount = -frame.PointsPrice,
                BalanceAfter = balances[member.MemberID],
                Type = "Redeem",
                RelatedFrameID = frame.FrameID,
                Note = $"兌換「{frame.Name}」",
                CreatedAt = redeemedAt
            });
            if (index < 100)
            {
                member.EquippedFrameID = frame.FrameID;
            }
        }

        for (var index = 250; index < 904; index++)
        {
            var member = persistedUsers[(index * 17) % persistedUsers.Count];
            var earn = index % 2 == 0;
            var amount = earn ? 50 + index % 5 * 25 : (index % 4 == 1 ? 100 : -50);
            balances[member.MemberID] += amount;
            transactions.Add(new PointsTransaction
            {
                MemberID = member.MemberID,
                Amount = amount,
                BalanceAfter = balances[member.MemberID],
                Type = earn ? "Earn" : "AdminAdjust",
                RelatedFrameID = null,
                Note = earn ? "完成美食地圖社群任務" : "營運人員調整點數",
                CreatedAt = now.AddDays(-(index % 365)).AddMinutes(index),
                CreatedBy = earn ? null : adminId
            });
        }

        foreach (var member in persistedUsers)
        {
            member.Points = balances[member.MemberID];
        }
        context.MemberAvatarFrames.AddRange(ownerships);
        context.PointsTransactions.AddRange(transactions);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task ValidateAsync(
        AppDbContext context,
        IWebHostEnvironment environment,
        string adminPassword)
    {
        var counts = await GetCountsAsync(context);
        var total = counts.Values.Sum();
        if (total != ExpectedRecordCount)
        {
            throw new InvalidOperationException(
                $"Formal data count verification failed: expected {ExpectedRecordCount}, found {total}. " +
                string.Join(", ", counts.Select(item => $"{item.Key}={item.Value}")));
        }

        var expectedCounts = new Dictionary<string, int>
        {
            ["UserLevels"] = 6,
            ["Members"] = 800,
            ["Tags"] = 40,
            ["Restaurants"] = 400,
            ["BusinessHours"] = 2_800,
            ["RestaurantTags"] = 1_200,
            ["Images"] = 620,
            ["RestaurantImages"] = 450,
            ["Reviews"] = 900,
            ["ReviewImages"] = 100,
            ["FavoriteFolders"] = 300,
            ["Favorites"] = 600,
            ["Reports"] = 300,
            ["Notifications"] = 300,
            ["AvatarFrames"] = 30,
            ["MemberAvatarFrames"] = 250,
            ["PointsTransactions"] = 904
        };
        var mismatches = expectedCounts
            .Where(item => counts[item.Key] != item.Value)
            .ToArray();
        if (mismatches.Length != 0)
        {
            throw new InvalidOperationException(
                "Formal table count verification failed: " +
                string.Join(", ", mismatches.Select(
                    item => $"{item.Key}={counts[item.Key]}, expected={item.Value}")));
        }

        var admin = await context.Members.SingleAsync(member => member.Email == AdminEmail);
        if (admin.UserName != "Admin" ||
            admin.NickName != "Admin" ||
            admin.Role != "Admin" ||
            !PasswordHashService.VerifyPassword(adminPassword, admin.PasswordHash))
        {
            throw new InvalidOperationException("Formal administrator verification failed.");
        }

        var customMemberAvatarCount = await context.Members.CountAsync(
            member => member.AvatarImageID != null);
        if (customMemberAvatarCount != ExpectedCustomMemberAvatarCount)
        {
            throw new InvalidOperationException(
                $"Formal member-avatar verification failed: expected {ExpectedCustomMemberAvatarCount}, " +
                $"found {customMemberAvatarCount}.");
        }

        var reportStatusCounts = await context.Reports
            .GroupBy(report => report.Status)
            .ToDictionaryAsync(group => group.Key, group => group.Count());
        if (reportStatusCounts.GetValueOrDefault("Pending") != 130 ||
            reportStatusCounts.GetValueOrDefault("Approved") != 100 ||
            reportStatusCounts.GetValueOrDefault("Rejected") != 70)
        {
            throw new InvalidOperationException("Report status coverage verification failed.");
        }

        var invalidTargets = await context.Reports.CountAsync(report =>
            (report.RestaurantID != null ? 1 : 0) +
            (report.ReviewID != null ? 1 : 0) +
            (report.ImageID != null ? 1 : 0) != 1);
        if (invalidTargets != 0)
        {
            throw new InvalidOperationException($"Report target verification failed: {invalidTargets} invalid rows.");
        }

        var approvedTargetNotDeleted =
            await context.Reports.CountAsync(report =>
                report.Status == "Approved" &&
                ((report.RestaurantID != null && !report.Restaurant!.IsDeleted) ||
                 (report.ReviewID != null && !report.Review!.IsDeleted) ||
                 (report.ImageID != null && !report.Image!.IsDeleted)));
        if (approvedTargetNotDeleted != 0)
        {
            throw new InvalidOperationException("Approved report target deletion verification failed.");
        }

        var firstRestaurantImageCount = await context.RestaurantImages
            .CountAsync(link => link.RestaurantID == 1);
        if (firstRestaurantImageCount != 20)
        {
            throw new InvalidOperationException(
                $"Restaurant gallery verification failed: expected 20 images, found {firstRestaurantImageCount}.");
        }

        var invalidFavorites = await context.Favorites
            .CountAsync(favorite => favorite.MemberID != favorite.FavoriteFolder!.MemberID);
        if (invalidFavorites != 0)
        {
            throw new InvalidOperationException("Favorite ownership verification failed.");
        }

        var imageUrls = await context.Images.Select(image => image.ImageURL).Distinct().ToArrayAsync();
        if (imageUrls.Length != ExpectedPhysicalImageCount)
        {
            throw new InvalidOperationException(
                $"Distinct image URL verification failed: expected {ExpectedPhysicalImageCount}, found {imageUrls.Length}.");
        }
        var missingFiles = imageUrls.Where(url => !File.Exists(Path.Combine(
            environment.WebRootPath,
            url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)))).ToArray();
        if (missingFiles.Length != 0)
        {
            throw new InvalidOperationException(
                "Formal image file verification failed: " + string.Join(", ", missingFiles));
        }
    }

    private static async Task<Dictionary<string, int>> GetCountsAsync(AppDbContext context) =>
        new()
        {
            ["UserLevels"] = await context.UserLevels.CountAsync(),
            ["Members"] = await context.Members.CountAsync(),
            ["Tags"] = await context.Tags.CountAsync(),
            ["Restaurants"] = await context.Restaurants.CountAsync(),
            ["BusinessHours"] = await context.BusinessHours.CountAsync(),
            ["RestaurantTags"] = await context.RestaurantTags.CountAsync(),
            ["Images"] = await context.Images.CountAsync(),
            ["RestaurantImages"] = await context.RestaurantImages.CountAsync(),
            ["Reviews"] = await context.Reviews.CountAsync(),
            ["ReviewImages"] = await context.ReviewImages.CountAsync(),
            ["FavoriteFolders"] = await context.FavoriteFolders.CountAsync(),
            ["Favorites"] = await context.Favorites.CountAsync(),
            ["Reports"] = await context.Reports.CountAsync(),
            ["Notifications"] = await context.Notifications.CountAsync(),
            ["AvatarFrames"] = await context.AvatarFrames.CountAsync(),
            ["MemberAvatarFrames"] = await context.MemberAvatarFrames.CountAsync(),
            ["PointsTransactions"] = await context.PointsTransactions.CountAsync()
        };
}
