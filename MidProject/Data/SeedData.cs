using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Services;
using MidProject.Services.IServices;

namespace MidProject.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ITaipeiClock>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var credentials = SeedDataCredentials.FromConfiguration(configuration);
        var now = clock.GetNow();

        await SeedUserLevelsAsync(context);
        await SeedMembersAsync(context, now, credentials);
    }

    public static async Task SeedUserLevelsAsync(AppDbContext context)
    {
        var definitions = new[]
        {
            ("新食客", 0, "基本會員權益"),
            ("尋味人", 500, "評論徽章"),
            ("品味家", 1300, "達人標章"),
            ("老饕客", 3400, "專屬活動邀請"),
            ("鑑味師", 8800, "VIP 標章"),
            ("食之神", 23000, "尊爵頭銜")
        };
        var existingLevels = await context.UserLevels.ToListAsync();

        foreach (var (name, minExp, rewards) in definitions)
        {
            if (existingLevels.Any(level =>
                    level.LevelName == name || level.MinExp == minExp))
            {
                continue;
            }

            var level = new UserLevel
            {
                LevelName = name,
                MinExp = minExp,
                Rewards = rewards
            };
            context.UserLevels.Add(level);
            existingLevels.Add(level);
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedMembersAsync(
        AppDbContext context,
        DateTime now,
        SeedDataCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var levels = await context.UserLevels.ToListAsync();
        var expectedMinimumExperience = new Dictionary<string, int>
        {
            ["新食客"] = 0,
            ["尋味人"] = 500,
            ["品味家"] = 1300,
            ["老饕客"] = 3400,
            ["鑑味師"] = 8800,
            ["食之神"] = 23000
        };
        var definitions = new[]
        {
            new MemberSeed("admin", "系統管理員", "admin@example.com", "Admin", "Normal", "品味家", 2000, 1000),
            new MemberSeed("Aiden_42", "怡安", "aiden@example.com", "User", "Normal", "尋味人", 620, 500),
            new MemberSeed("Mason17", "小森", "mason@example.com", "User", "Normal", "新食客", 280, 200),
            new MemberSeed("Ethan.85", "亦辰", "ethan@example.com", "User", "Warning", "新食客", 180, 100)
        };
        var existingEmails = (await context.Members
            .Select(member => member.Email)
            .ToListAsync()).ToHashSet();

        foreach (var definition in definitions)
        {
            if (existingEmails.Contains(definition.Email))
            {
                continue;
            }

            context.Members.Add(new Member
            {
                UserName = definition.UserName,
                NickName = definition.NickName,
                Email = definition.Email,
                PasswordHash = PasswordHashService.HashPassword(
                    definition.Role == "Admin"
                        ? credentials.AdminPassword
                        : credentials.UserPassword),
                Role = definition.Role,
                Status = definition.Status,
                WarningCount = definition.Status == "Warning" ? 1 : 0,
                AdminNote = definition.Status == "Warning" ? "評論用語需注意" : null,
                LevelID = levels.First(level =>
                    level.LevelName == definition.LevelName ||
                    level.MinExp == expectedMinimumExperience[definition.LevelName]).LevelID,
                Experience = definition.Experience,
                Points = definition.Points,
                CreatedAt = now.AddDays(-30),
                UpdatedAt = now.AddDays(-1)
            });
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedRestaurantsAsync(AppDbContext context, DateTime now)
    {
        var admin = await RequiredMemberAsync(context, "admin@example.com");
        var tagNames = new[] { "早午餐", "咖啡廳", "義式料理", "甜點" };
        var existingTags = await context.Tags
            .Where(tag => tagNames.Contains(tag.TagName))
            .ToDictionaryAsync(tag => tag.TagName);
        var nextSortOrder = (await context.Tags
            .Select(tag => (int?)tag.SortOrder)
            .MaxAsync() ?? -1) + 1;
        foreach (var name in tagNames)
        {
            if (!existingTags.ContainsKey(name))
            {
                var tag = new Tag { TagName = name, SortOrder = nextSortOrder++ };
                context.Tags.Add(tag);
                existingTags[name] = tag;
            }
        }

        await context.SaveChangesAsync();

        var definitions = new[]
        {
            new RestaurantSeed(
                "木柵早午餐", "台北市", "文山區", "木柵路一段 10 號",
                "02-2939-5510", "週末人潮較多。", false, new[] { "早午餐", "咖啡廳" }),
            new RestaurantSeed(
                "北投義麵坊", "台北市", "北投區", "光明路 22 號",
                "02-2891-6620", "義大利麵與燉飯。", false, new[] { "義式料理" }),
            new RestaurantSeed(
                "晴光甜點室", "台北市", "中山區", "雙城街 12 號",
                "02-2599-2300", "店家已結束營業。", true, new[] { "甜點", "咖啡廳" })
        };

        foreach (var definition in definitions)
        {
            var restaurant = await context.Restaurants
                .FirstOrDefaultAsync(item => item.Name == definition.Name);
            if (restaurant is null)
            {
                restaurant = new Restaurant
                {
                    Name = definition.Name,
                    City = definition.City,
                    District = definition.District,
                    DetailedAddress = definition.Address,
                    Phone = definition.Phone,
                    Note = definition.Note,
                    MemberID = admin.MemberID,
                    CreatedAt = now.AddDays(-20),
                    UpdatedAt = now.AddDays(-1),
                    IsDeleted = definition.IsDeleted,
                    DeletedAt = definition.IsDeleted ? now.AddDays(-2) : null,
                    DeletedBy = definition.IsDeleted ? admin.MemberID : null,
                    DeleteReason = definition.IsDeleted ? "已歇業" : null
                };
                context.Restaurants.Add(restaurant);
                await context.SaveChangesAsync();
            }

            if (!await context.BusinessHours.AnyAsync(
                    hour => hour.RestaurantID == restaurant.RestaurantID))
            {
                AddBusinessHours(context, restaurant.RestaurantID);
            }

            foreach (var tagName in definition.Tags)
            {
                var tag = existingTags[tagName];
                if (!await context.RestaurantTags.AnyAsync(
                        link => link.RestaurantID == restaurant.RestaurantID &&
                                link.TagID == tag.TagID))
                {
                    context.RestaurantTags.Add(new RestaurantTag
                    {
                        RestaurantID = restaurant.RestaurantID,
                        TagID = tag.TagID
                    });
                }
            }
        }

        await SeedImageAsync(
            context,
            admin.MemberID,
            "/uploads/MemberAvatar/avatar24-01.jpg",
            "MemberAvatar",
            now);
        await context.SaveChangesAsync();

        var avatar = await context.Images.SingleAsync(
            image => image.ImageURL == "/uploads/MemberAvatar/avatar24-01.jpg");
        if (admin.AvatarImageID is null)
        {
            admin.AvatarImageID = avatar.ImageID;
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedReviewsAsync(AppDbContext context, DateTime now)
    {
        var admin = await RequiredMemberAsync(context, "admin@example.com");
        var aiden = await RequiredMemberAsync(context, "aiden@example.com");
        var mason = await RequiredMemberAsync(context, "mason@example.com");
        var brunch = await RequiredRestaurantAsync(context, "木柵早午餐");
        var pasta = await RequiredRestaurantAsync(context, "北投義麵坊");
        var definitions = new[]
        {
            new ReviewSeed(aiden.MemberID, brunch.RestaurantID, 5, "餐點份量足，適合週末聚餐。", "Active"),
            new ReviewSeed(mason.MemberID, brunch.RestaurantID, 4, "服務速度普通，但餐點味道不錯。", "Active"),
            new ReviewSeed(aiden.MemberID, pasta.RestaurantID, 3, "環境乾淨，座位稍微擁擠。", "PendingReview")
        };

        foreach (var definition in definitions)
        {
            if (!await context.Reviews.AnyAsync(review => review.Content == definition.Content))
            {
                context.Reviews.Add(new Review
                {
                    MemberID = definition.MemberID,
                    RestaurantID = definition.RestaurantID,
                    Rating = definition.Rating,
                    Content = definition.Content,
                    Status = definition.Status,
                    CreatedAt = now.AddDays(-7),
                    UpdatedAt = now.AddDays(-7).AddHours(1)
                });
            }
        }

        await context.SaveChangesAsync();

        var imageDefinitions = new[]
        {
            (Url: "/uploads/ReviewImage/義大利麵.jpg", Content: "環境乾淨，座位稍微擁擠。"),
            (Url: "/uploads/ReviewImage/蛋餅.jpg", Content: "餐點份量足，適合週末聚餐。")
        };
        foreach (var definition in imageDefinitions)
        {
            await SeedImageAsync(context, admin.MemberID, definition.Url, "ReviewImage", now);
            await context.SaveChangesAsync();

            var image = await context.Images.SingleAsync(item => item.ImageURL == definition.Url);
            var review = await context.Reviews.SingleAsync(item => item.Content == definition.Content);
            if (!await context.ReviewImages.AnyAsync(
                    link => link.ReviewID == review.ReviewID && link.ImageID == image.ImageID))
            {
                context.ReviewImages.Add(new ReviewImage
                {
                    ReviewID = review.ReviewID,
                    ImageID = image.ImageID
                });
            }
        }

        await context.SaveChangesAsync();
        await RecalculateRestaurantStatsAsync(context, brunch.RestaurantID, pasta.RestaurantID);
    }

    public static async Task SeedFavoritesAsync(AppDbContext context, DateTime now)
    {
        var aiden = await RequiredMemberAsync(context, "aiden@example.com");
        var brunch = await RequiredRestaurantAsync(context, "木柵早午餐");
        var folder = await context.FavoriteFolders.FirstOrDefaultAsync(
            item => item.MemberID == aiden.MemberID && item.FolderName == "週末想吃");
        if (folder is null)
        {
            folder = new FavoriteFolder
            {
                MemberID = aiden.MemberID,
                FolderName = "週末想吃",
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now.AddDays(-1)
            };
            context.FavoriteFolders.Add(folder);
            await context.SaveChangesAsync();
        }

        if (!await context.Favorites.AnyAsync(
                item => item.MemberID == aiden.MemberID &&
                        item.RestaurantID == brunch.RestaurantID))
        {
            context.Favorites.Add(new Favorite
            {
                MemberID = aiden.MemberID,
                RestaurantID = brunch.RestaurantID,
                FavoriteFolderID = folder.FavoriteFolderID,
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-5)
            });
            await context.SaveChangesAsync();
        }
    }

    public static async Task SeedReportsAsync(AppDbContext context, DateTime now)
    {
        var admin = await RequiredMemberAsync(context, "admin@example.com");
        var aiden = await RequiredMemberAsync(context, "aiden@example.com");
        var mason = await RequiredMemberAsync(context, "mason@example.com");
        var brunch = await RequiredRestaurantAsync(context, "木柵早午餐");
        var pendingReview = await context.Reviews.SingleAsync(
            review => review.Content == "環境乾淨，座位稍微擁擠。");
        var definitions = new[]
        {
            new ReportSeed("評論內容可能包含不適當用語，請協助審核。", "人身攻擊", "Pending", null, pendingReview.ReviewID),
            new ReportSeed("店家營業資訊與現場公告不一致。", "不實資訊", "Approved", brunch.RestaurantID, null),
            new ReportSeed("評論內容與實際用餐經驗相關，未發現違規。", "垃圾訊息", "Rejected", null, pendingReview.ReviewID)
        };

        foreach (var definition in definitions)
        {
            if (await context.Reports.AnyAsync(report => report.Reason == definition.Reason))
            {
                continue;
            }

            var handled = definition.Status != "Pending";
            context.Reports.Add(new Report
            {
                ReporterMemberID = definition.Status == "Rejected"
                    ? mason.MemberID
                    : aiden.MemberID,
                RestaurantID = definition.RestaurantID,
                ReviewID = definition.ReviewID,
                Reason = definition.Reason,
                Category = definition.Category,
                Status = definition.Status,
                CreatedAt = now.AddDays(-3),
                HandledAt = handled ? now.AddDays(-2) : null,
                HandledByMemberID = handled ? admin.MemberID : null,
                AdminNote = handled ? "已完成內容查核與案件紀錄。" : null
            });
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedNotificationsAsync(AppDbContext context, DateTime now)
    {
        var admin = await RequiredMemberAsync(context, "admin@example.com");
        var aiden = await RequiredMemberAsync(context, "aiden@example.com");
        const string title = "歡迎加入美食地圖";
        const string content = "您的會員帳號已啟用，現在可以收藏餐廳並分享用餐心得。";

        if (!await context.Notifications.AnyAsync(
                item => item.MemberID == aiden.MemberID &&
                        item.Title == title &&
                        item.Content == content))
        {
            context.Notifications.Add(new Notification
            {
                MemberID = aiden.MemberID,
                NotificationType = "Personal",
                Title = title,
                Content = content,
                ScheduledAt = now,
                IsSent = false,
                CreatedAt = now,
                CreatedBy = admin.MemberID
            });
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedImageAsync(
        AppDbContext context,
        int uploadedByMemberId,
        string imageUrl,
        string imageType,
        DateTime now)
    {
        if (!await context.Images.AnyAsync(image => image.ImageURL == imageUrl))
        {
            context.Images.Add(new Image
            {
                UploadedByMemberID = uploadedByMemberId,
                ImageURL = imageUrl,
                ImageType = imageType,
                UploadedAt = now.AddDays(-2)
            });
        }
    }

    private static async Task RecalculateRestaurantStatsAsync(
        AppDbContext context,
        params int[] restaurantIds)
    {
        foreach (var restaurantId in restaurantIds)
        {
            var restaurant = await context.Restaurants.FindAsync(restaurantId);
            if (restaurant is null)
            {
                continue;
            }

            var ratings = await context.Reviews
                .Where(review =>
                    review.RestaurantID == restaurantId &&
                    !review.IsDeleted &&
                    review.Status == "Active")
                .Select(review => review.Rating)
                .ToListAsync();
            restaurant.ReviewCount = ratings.Count;
            restaurant.AverageRating = ratings.Count == 0
                ? 0
                : Math.Round((decimal)ratings.Average(), 2);
        }

        await context.SaveChangesAsync();
    }

    private static async Task<Member> RequiredMemberAsync(
        AppDbContext context,
        string email)
    {
        return await context.Members.SingleAsync(member => member.Email == email);
    }

    private static async Task<Restaurant> RequiredRestaurantAsync(
        AppDbContext context,
        string name)
    {
        return await context.Restaurants.SingleAsync(restaurant => restaurant.Name == name);
    }

    private static void AddBusinessHours(AppDbContext context, int restaurantId)
    {
        for (var day = 1; day <= 7; day++)
        {
            context.BusinessHours.Add(new BusinessHour
            {
                RestaurantID = restaurantId,
                DayOfWeek = day,
                OpenTime = day == 7 ? TimeOnly.MinValue : new TimeOnly(11, 0),
                CloseTime = day == 7 ? TimeOnly.MinValue : new TimeOnly(21, 0),
                IsClosed = day == 7
            });
        }
    }

    private sealed record MemberSeed(
        string UserName,
        string NickName,
        string Email,
        string Role,
        string Status,
        string LevelName,
        int Experience,
        int Points);

    private sealed record RestaurantSeed(
        string Name,
        string City,
        string District,
        string Address,
        string Phone,
        string Note,
        bool IsDeleted,
        string[] Tags);

    private sealed record ReviewSeed(
        int MemberID,
        int RestaurantID,
        int Rating,
        string Content,
        string Status);

    private sealed record ReportSeed(
        string Reason,
        string Category,
        string Status,
        int? RestaurantID,
        int? ReviewID);
}
