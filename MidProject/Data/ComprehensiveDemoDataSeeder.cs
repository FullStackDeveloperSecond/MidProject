using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Services;
using MidProject.Services.IServices;

namespace MidProject.Data;

public static class ComprehensiveDemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ITaipeiClock>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var credentials = SeedDataCredentials.FromConfiguration(configuration);
        var now = clock.GetNow();

        var admin = await context.Members.SingleAsync(member => member.Email == "admin@example.com");
        await SeedMembersAsync(context, credentials, admin.MemberID, now);
        await SeedTagsAsync(context, admin.MemberID, now);
        await SeedRestaurantsAsync(context, admin.MemberID, now);
        await SeedImagesAsync(context, admin.MemberID, now);
        await SeedReviewsAsync(context, admin.MemberID, now);
        await SeedFavoritesAsync(context, admin.MemberID, now);
        await SeedNotificationsAsync(context, admin.MemberID, now);
        await SeedPointTransactionsAsync(context, admin.MemberID, now);
        await RecalculateRestaurantStatsAsync(context);
    }

    private static async Task SeedMembersAsync(
        AppDbContext context,
        SeedDataCredentials credentials,
        int adminMemberId,
        DateTime now)
    {
        var levels = await context.UserLevels.OrderBy(level => level.MinExp).ToListAsync();
        var definitions = new[]
        {
            new MemberSeed("operations.admin", "營運管理員", "demo.qa.admin@example.com", "Admin", "Normal", 4, 4200, 1500),
            new MemberSeed("foodie.yawen", "雅雯", "demo.foodie01@example.com", "User", "Normal", 2, 760, 880),
            new MemberSeed("coffee.ziqian", "子謙", "demo.foodie02@example.com", "User", "Normal", 1, 320, 430),
            new MemberSeed("foodie.yijun", "怡君", "demo.warning@example.com", "User", "Warning", 1, 180, 250),
            new MemberSeed("foodie.bohan", "柏翰", "demo.muted@example.com", "User", "Muted", 1, 120, 160),
            new MemberSeed("foodie.jiahao", "家豪", "demo.suspended@example.com", "User", "Suspended", 1, 90, 200),
            new MemberSeed("closed.account", "已註銷會員", "demo.deleted@example.com", "User", "Deleted", 1, 50, 50),
            new MemberSeed("limited.account", "登入受限會員", "demo.locked@example.com", "User", "Normal", 1, 210, 300)
        };

        foreach (var definition in definitions)
        {
            var member = await context.Members
                .SingleOrDefaultAsync(item => item.Email == definition.Email);
            var isNew = member is null;
            member ??= new Member();

            member.UserName = definition.UserName;
            member.NickName = definition.NickName;
            member.Email = definition.Email;
            if (isNew)
            {
                member.PasswordHash = PasswordHashService.HashPassword(
                    definition.Role == "Admin"
                        ? credentials.AdminPassword
                        : credentials.UserPassword);
                member.CreatedAt = now.AddDays(-45);
                context.Members.Add(member);
            }

            member.Role = definition.Role;
            member.Status = definition.Status;
            member.IsActive = definition.Status != "Deleted";
            member.IsDeleted = definition.Status == "Deleted";
            member.DeletedAt = member.IsDeleted ? now.AddDays(-5) : null;
            member.DeletedBy = member.IsDeleted ? adminMemberId : null;
            member.IsLocked = definition.Email == "demo.locked@example.com";
            member.FailedLoginCount = member.IsLocked ? 5 : 0;
            member.LoginLockoutEndAt = member.IsLocked ? now.AddMinutes(15) : null;
            member.PenaltyEndAt = definition.Status switch
            {
                "Muted" => now.AddDays(3),
                "Suspended" => now.AddDays(14),
                _ => null
            };
            member.AdminNote = definition.Status switch
            {
                "Warning" => "經客服確認收到一次內容警告。",
                "Muted" => "因多次不當留言，暫停發言至期限屆滿。",
                "Suspended" => "因嚴重違反社群規範，帳號暫停使用。",
                "Deleted" => "會員申請註銷帳號，資料已依規定停用。",
                _ => null
            };
            member.WarningCount = definition.Status is "Warning" or "Muted" or "Suspended" ? 1 : 0;
            member.LevelID = levels[Math.Min(definition.LevelIndex, levels.Count - 1)].LevelID;
            member.Experience = definition.Experience;
            member.Points = definition.Points;
            member.Birthday = new DateOnly(1990 + definition.LevelIndex, definition.LevelIndex + 1, 10);
            member.Phone = $"0912-00{definition.LevelIndex:D2}";
            member.UpdatedAt = now.AddDays(-1);
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedTagsAsync(AppDbContext context, int adminMemberId, DateTime now)
    {
        var definitions = new[]
        {
            new TagSeed("日式料理", false),
            new TagSeed("台灣小吃", false),
            new TagSeed("咖哩", false),
            new TagSeed("咖啡廳", false),
            new TagSeed("蔬食", false),
            new TagSeed("深夜食堂", false),
            new TagSeed("季節限定", true)
        };
        var nextSortOrder = (await context.Tags.Select(tag => (int?)tag.SortOrder).MaxAsync() ?? -1) + 1;

        foreach (var definition in definitions)
        {
            var tag = await context.Tags.SingleOrDefaultAsync(item => item.TagName == definition.Name);
            if (tag is null)
            {
                tag = new Tag { TagName = definition.Name, SortOrder = nextSortOrder++ };
                context.Tags.Add(tag);
            }

            tag.IsDeleted = definition.IsDeleted;
            tag.DeletedAt = definition.IsDeleted ? now.AddDays(-4) : null;
            tag.DeletedBy = definition.IsDeleted ? adminMemberId : null;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedRestaurantsAsync(AppDbContext context, int adminMemberId, DateTime now)
    {
        var owners = await context.Members
            .Where(member => member.Email == "demo.qa.admin@example.com" ||
                             member.Email == "demo.foodie01@example.com")
            .ToDictionaryAsync(member => member.Email);
        var tags = await context.Tags.ToDictionaryAsync(tag => tag.TagName);
        var definitions = new[]
        {
            new RestaurantSeed(
                "信義夜食堂", "台北市", "信義區", "松仁路 100 號", "02-2720-1001",
                25.033210m, 121.568330m, false, null,
                "demo.qa.admin@example.com", ["日式料理", "深夜食堂"]),
            new RestaurantSeed(
                "大安咖哩研究所", "台北市", "大安區", "復興南路一段 200 號", "02-2700-2002",
                25.026770m, 121.543410m, false, null,
                "demo.foodie01@example.com", ["咖哩", "日式料理"]),
            new RestaurantSeed(
                "松山蔬食小館", "台北市", "松山區", "南京東路五段 88 號", "02-2760-3003",
                25.051620m, 121.563680m, false, null,
                "demo.qa.admin@example.com", ["蔬食", "台灣小吃"]),
            new RestaurantSeed(
                "中山深夜咖啡", "台北市", "中山區", "林森北路 66 號", "02-2560-4004",
                25.054180m, 121.525040m, false, null,
                "demo.foodie01@example.com", ["咖啡廳", "深夜食堂"]),
            new RestaurantSeed(
                "萬華古早味麵店", "台北市", "萬華區", "西園路一段 50 號", "02-2300-5005",
                25.036420m, 121.499910m, true, "店家已結束營業",
                "demo.qa.admin@example.com", ["台灣小吃", "季節限定"])
        };
        var restaurantNotes = new[]
        {
            "供應日式定食與晚間限定料理，週六採午、晚兩段營業。",
            "主打慢熬咖哩與季節蔬菜，可依需求調整辣度。",
            "以在地蔬菜設計家常套餐，每週日公休。",
            "提供單品咖啡、甜點與夜間輕食，每週日公休。",
            "店家已結束營業，資料保留供歷史紀錄查詢。"
        };

        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];
            var restaurant = await context.Restaurants
                .SingleOrDefaultAsync(item => item.Name == definition.Name);
            var isNew = restaurant is null;
            restaurant ??= new Restaurant();

            restaurant.Name = definition.Name;
            restaurant.City = definition.City;
            restaurant.District = definition.District;
            restaurant.DetailedAddress = definition.Address;
            restaurant.Phone = definition.Phone;
            restaurant.Note = restaurantNotes[index];
            restaurant.Latitude = definition.Latitude;
            restaurant.Longitude = definition.Longitude;
            restaurant.MemberID = owners[definition.OwnerEmail].MemberID;
            restaurant.IsDeleted = definition.IsDeleted;
            restaurant.DeletedAt = definition.IsDeleted ? now.AddDays(-3) : null;
            restaurant.DeletedBy = definition.IsDeleted ? adminMemberId : null;
            restaurant.DeleteReason = definition.IsDeleted ? definition.DeleteReason : null;
            restaurant.UpdatedAt = now.AddDays(-index);

            if (isNew)
            {
                restaurant.CreatedAt = now.AddDays(-30 + index);
                context.Restaurants.Add(restaurant);
                await context.SaveChangesAsync();
            }

            if (!await context.BusinessHours.AnyAsync(hour => hour.RestaurantID == restaurant.RestaurantID))
            {
                AddBusinessHours(context, restaurant.RestaurantID, splitSaturday: index == 0);
            }

            foreach (var tagName in definition.Tags)
            {
                var tag = tags[tagName];
                if (!await context.RestaurantTags.AnyAsync(link =>
                        link.RestaurantID == restaurant.RestaurantID && link.TagID == tag.TagID))
                {
                    context.RestaurantTags.Add(new RestaurantTag
                    {
                        RestaurantID = restaurant.RestaurantID,
                        TagID = tag.TagID
                    });
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedImagesAsync(AppDbContext context, int adminMemberId, DateTime now)
    {
        var restaurantNames = new[]
        {
            "信義夜食堂",
            "大安咖哩研究所",
            "松山蔬食小館",
            "中山深夜咖啡",
            "萬華古早味麵店"
        };
        var restaurants = await context.Restaurants
            .Where(restaurant => restaurantNames.Contains(restaurant.Name))
            .ToListAsync();
        restaurants = restaurantNames
            .Select(name => restaurants.Single(restaurant => restaurant.Name == name))
            .ToList();
        var members = await context.Members
            .Where(member => member.Email.StartsWith("demo."))
            .ToDictionaryAsync(member => member.Email);

        var coverUrls = Enumerable.Range(1, 5)
            .Select(index => $"/uploads/RestaurantCover/demo-cover-{index:D2}.jpg")
            .ToArray();
        for (var index = 0; index < restaurants.Count; index++)
        {
            var image = await UpsertImageAsync(
                context, adminMemberId, coverUrls[index], "RestaurantCover", index, now);
            await AddRestaurantImageLinkAsync(context, restaurants[index].RestaurantID, image.ImageID);
        }

        var environmentUrls = Enumerable.Range(1, 3)
            .Select(index => $"/uploads/RestaurantEnvironment/demo-environment-{index:D2}.jpg")
            .ToArray();
        for (var index = 0; index < environmentUrls.Length; index++)
        {
            var image = await UpsertImageAsync(
                context, adminMemberId, environmentUrls[index], "RestaurantEnvironment", index, now);
            await AddRestaurantImageLinkAsync(context, restaurants[index].RestaurantID, image.ImageID);
        }

        var avatarAssignments = new[]
        {
            ("demo.qa.admin@example.com", "/uploads/MemberAvatar/demo-avatar-01.jpg"),
            ("demo.foodie01@example.com", "/uploads/MemberAvatar/demo-avatar-02.jpg"),
            ("demo.foodie02@example.com", "/uploads/MemberAvatar/demo-avatar-03.jpg")
        };
        foreach (var (email, url) in avatarAssignments)
        {
            var member = members[email];
            var image = await UpsertImageAsync(context, member.MemberID, url, "MemberAvatar", 0, now);
            member.AvatarImageID = image.ImageID;
        }

        var frames = await context.AvatarFrames.OrderBy(frame => frame.SortOrder).Take(3).ToListAsync();
        for (var index = 0; index < frames.Count; index++)
        {
            var url = $"/uploads/AvatarFrame/demo-frame-{index + 1:D2}.jpg";
            var image = await UpsertImageAsync(
                context, adminMemberId, url, "AvatarFrame", index, now);
            frames[index].ImageID = image.ImageID;
            frames[index].UpdatedAt = now;
        }

        foreach (var index in Enumerable.Range(1, 4))
        {
            await UpsertImageAsync(
                context,
                adminMemberId,
                $"/uploads/ReviewImage/demo-review-{index:D2}.jpg",
                "ReviewImage",
                index,
                now);
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedReviewsAsync(AppDbContext context, int adminMemberId, DateTime now)
    {
        var members = await context.Members
            .Where(member => member.Email.StartsWith("demo."))
            .ToDictionaryAsync(member => member.Email);
        var restaurants = await context.Restaurants
            .Where(restaurant => restaurant.Name == "信義夜食堂" ||
                                 restaurant.Name == "大安咖哩研究所" ||
                                 restaurant.Name == "松山蔬食小館" ||
                                 restaurant.Name == "中山深夜咖啡")
            .ToDictionaryAsync(restaurant => restaurant.Name);
        var definitions = new[]
        {
            new ReviewSeed("宵夜定食很有特色，烤物火候也恰到好處。", "demo.foodie01@example.com", "信義夜食堂", 5, "Active", false),
            new ReviewSeed("服務快速、座位舒適，下班後聚餐很方便。", "demo.foodie02@example.com", "信義夜食堂", 4, "Active", false),
            new ReviewSeed("咖哩香氣很足，辣度與配菜搭配得剛好。", "demo.warning@example.com", "大安咖哩研究所", 5, "Active", false),
            new ReviewSeed("這次出餐時間偏久，希望尖峰時段能改善。", "demo.foodie01@example.com", "大安咖哩研究所", 2, "PendingReview", false),
            new ReviewSeed("蔬食選擇比預期豐富，調味有層次又不油膩。", "demo.foodie02@example.com", "松山蔬食小館", 4, "Active", false),
            new ReviewSeed("口味清爽、份量適中，適合平日午餐。", "demo.warning@example.com", "松山蔬食小館", 3, "Active", false),
            new ReviewSeed("深夜來訪仍有穩定的咖啡品質，店員也很親切。", "demo.foodie01@example.com", "中山深夜咖啡", 5, "Active", false),
            new ReviewSeed("甜點稍微偏甜，但環境安靜、座位寬敞。", "demo.foodie02@example.com", "中山深夜咖啡", 3, "Active", false),
            new ReviewSeed("餐點與菜單照片差異較大，已送交平台確認。", "demo.muted@example.com", "信義夜食堂", 1, "PendingReview", false),
            new ReviewSeed("服務態度需要改善，不會再次造訪。", "demo.suspended@example.com", "大安咖哩研究所", 2, "Active", true),
            new ReviewSeed("再次造訪仍然滿意，晚間限定菜色值得推薦。", "demo.warning@example.com", "信義夜食堂", 4, "Active", false),
            new ReviewSeed("餐桌空間舒服，套餐很適合朋友一起分享。", "demo.foodie01@example.com", "松山蔬食小館", 5, "Active", false)
        };

        foreach (var definition in definitions)
        {
            var review = await context.Reviews
                .SingleOrDefaultAsync(item => item.Content == definition.Content);
            var isNew = review is null;
            review ??= new Review();
            review.MemberID = members[definition.MemberEmail].MemberID;
            review.RestaurantID = restaurants[definition.RestaurantName].RestaurantID;
            review.Rating = definition.Rating;
            review.Content = definition.Content;
            review.Status = definition.Status;
            review.IsDeleted = definition.IsDeleted;
            review.DeletedAt = definition.IsDeleted ? now.AddDays(-2) : null;
            review.DeletedBy = definition.IsDeleted ? adminMemberId : null;
            review.UpdatedAt = now.AddDays(-1);
            if (isNew)
            {
                review.CreatedAt = now.AddDays(-20 + Array.IndexOf(definitions, definition));
                context.Reviews.Add(review);
            }
        }

        await context.SaveChangesAsync();

        var reviewImages = await context.Images
            .Where(image => image.ImageURL.StartsWith("/uploads/ReviewImage/demo-review-"))
            .OrderBy(image => image.ImageURL)
            .ToListAsync();
        var imageReviewContents = definitions
            .Take(reviewImages.Count)
            .Select(definition => definition.Content)
            .ToArray();
        var imageReviewRows = await context.Reviews
            .Where(review => review.Content != null &&
                             imageReviewContents.Contains(review.Content))
            .ToListAsync();
        var imageReviews = imageReviewContents
            .Select(content => imageReviewRows.Single(review => review.Content == content))
            .ToList();
        for (var index = 0; index < reviewImages.Count; index++)
        {
            if (!await context.ReviewImages.AnyAsync(link =>
                    link.ReviewID == imageReviews[index].ReviewID &&
                    link.ImageID == reviewImages[index].ImageID))
            {
                context.ReviewImages.Add(new ReviewImage
                {
                    ReviewID = imageReviews[index].ReviewID,
                    ImageID = reviewImages[index].ImageID
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedFavoritesAsync(AppDbContext context, int adminMemberId, DateTime now)
    {
        var members = await context.Members
            .Where(member => member.Email == "demo.foodie01@example.com" ||
                             member.Email == "demo.foodie02@example.com" ||
                             member.Email == "demo.warning@example.com")
            .ToDictionaryAsync(member => member.Email);
        var restaurantNames = new[]
        {
            "信義夜食堂",
            "大安咖哩研究所",
            "松山蔬食小館",
            "中山深夜咖啡"
        };
        var restaurantRows = await context.Restaurants
            .Where(restaurant => restaurantNames.Contains(restaurant.Name))
            .ToListAsync();
        var restaurants = restaurantNames
            .Select(name => restaurantRows.Single(restaurant => restaurant.Name == name))
            .ToArray();
        var folderDefinitions = new[]
        {
            new FolderSeed("demo.foodie01@example.com", "本週想吃", false),
            new FolderSeed("demo.foodie02@example.com", "約會清單", false),
            new FolderSeed("demo.warning@example.com", "已刪除收藏夾", true)
        };

        var folders = new Dictionary<string, FavoriteFolder>();
        foreach (var definition in folderDefinitions)
        {
            var member = members[definition.MemberEmail];
            var folder = await context.FavoriteFolders.SingleOrDefaultAsync(item =>
                item.MemberID == member.MemberID && item.FolderName == definition.Name);
            if (folder is null)
            {
                folder = new FavoriteFolder
                {
                    MemberID = member.MemberID,
                    FolderName = definition.Name,
                    CreatedAt = now.AddDays(-15)
                };
                context.FavoriteFolders.Add(folder);
                await context.SaveChangesAsync();
            }

            folder.IsDeleted = definition.IsDeleted;
            folder.DeletedAt = definition.IsDeleted ? now.AddDays(-2) : null;
            folder.DeletedBy = definition.IsDeleted ? adminMemberId : null;
            folder.UpdatedAt = now.AddDays(-1);
            folders[definition.Name] = folder;
            folders[definition.MemberEmail] = folder;
        }

        var desiredKeys = new HashSet<(int MemberID, int RestaurantID, int FavoriteFolderID)>();
        for (var index = 0; index < 8; index++)
        {
            var member = index % 2 == 0
                ? members["demo.foodie01@example.com"]
                : members["demo.foodie02@example.com"];
            var folder = index % 2 == 0 ? folders["本週想吃"] : folders["約會清單"];
            var restaurant = restaurants[index / 2];
            desiredKeys.Add((member.MemberID, restaurant.RestaurantID, folder.FavoriteFolderID));
            var favorite = await context.Favorites
                .OrderBy(item => item.FavoriteID)
                .FirstOrDefaultAsync(item =>
                item.MemberID == member.MemberID &&
                item.RestaurantID == restaurant.RestaurantID &&
                item.FavoriteFolderID == folder.FavoriteFolderID);
            if (favorite is null)
            {
                favorite = new Favorite
                {
                    MemberID = member.MemberID,
                    RestaurantID = restaurant.RestaurantID,
                    FavoriteFolderID = folder.FavoriteFolderID,
                    CreatedAt = now.AddDays(-10 + index)
                };
                context.Favorites.Add(favorite);
            }

            var isDeleted = index == 7;
            favorite.IsDeleted = isDeleted;
            favorite.DeletedAt = isDeleted ? now.AddDays(-1) : null;
            favorite.DeletedBy = isDeleted ? adminMemberId : null;
            favorite.UpdatedAt = now.AddDays(-1);
        }

        await context.SaveChangesAsync();

        var demoFolderIds = folders.Values
            .Select(folder => folder.FavoriteFolderID)
            .Distinct()
            .ToArray();
        var demoFavorites = await context.Favorites
            .Where(item => demoFolderIds.Contains(item.FavoriteFolderID))
            .OrderBy(item => item.FavoriteID)
            .ToListAsync();
        var favoritesToRemove = demoFavorites
            .GroupBy(item => (item.MemberID, item.RestaurantID, item.FavoriteFolderID))
            .SelectMany(group => desiredKeys.Contains(group.Key) ? group.Skip(1) : group)
            .ToList();
        if (favoritesToRemove.Count > 0)
        {
            context.Favorites.RemoveRange(favoritesToRemove);
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedNotificationsAsync(AppDbContext context, int adminMemberId, DateTime now)
    {
        var members = await context.Members
            .Where(member => member.Email.StartsWith("demo."))
            .ToDictionaryAsync(member => member.Email);
        var levels = await context.UserLevels.OrderBy(level => level.MinExp).ToListAsync();
        var definitions = new[]
        {
            new NotificationSeed("收藏清單提醒", "您收藏的餐廳本週新增了營業資訊，出發前別忘了確認。", "Personal", "demo.foodie01@example.com", null, null, null, false, false, now.AddHours(2)),
            new NotificationSeed("本週美食推薦", "根據您的收藏偏好，為您整理了三間本週人氣餐廳。", "Personal", "demo.foodie02@example.com", null, null, null, true, false, now.AddDays(-2)),
            new NotificationSeed("帳號安全提醒", "我們偵測到新的登入活動，如非本人操作請立即更新密碼。", "Personal", "demo.warning@example.com", null, null, null, false, true, now.AddDays(1)),
            new NotificationSeed("平台功能更新", "餐廳收藏與評論圖片功能已更新，歡迎登入查看。", "Condition", null, "User", null, null, false, false, now.AddHours(4)),
            new NotificationSeed("社群活動開跑", "本月完成三篇用餐心得，即可獲得額外會員點數。", "Condition", null, null, "Normal", null, true, false, now.AddDays(-1)),
            new NotificationSeed("社群規範提醒", "請共同維護友善交流空間，發布內容前請再次確認社群規範。", "Condition", null, null, "Warning", null, false, false, now.AddHours(6)),
            new NotificationSeed("等級升級專屬好禮", "尋味人以上會員可於本週兌換限定頭像外框。", "Condition", null, null, null, levels[1].LevelID, false, false, now.AddHours(8)),
            new NotificationSeed("營運公告", "管理後台將於週三凌晨進行例行維護，預計三十分鐘完成。", "Condition", null, "Admin", null, null, true, false, now.AddDays(-3))
        };

        foreach (var definition in definitions)
        {
            var notification = await context.Notifications
                .SingleOrDefaultAsync(item => item.Title == definition.Title);
            notification ??= new Notification();
            var isNew = notification.NotificationID == 0;
            notification.MemberID = definition.MemberEmail is null
                ? null
                : members[definition.MemberEmail].MemberID;
            notification.NotificationType = definition.Type;
            notification.TargetRole = definition.TargetRole;
            notification.TargetStatus = definition.TargetStatus;
            notification.TargetLevelID = definition.TargetLevelId;
            notification.Title = definition.Title;
            notification.Content = definition.Content;
            notification.ScheduledAt = definition.ScheduledAt;
            notification.IsSent = definition.IsSent;
            notification.SentAt = definition.IsSent ? definition.ScheduledAt : null;
            notification.IsDeleted = definition.IsDeleted;
            notification.DeletedAt = definition.IsDeleted ? now.AddHours(-1) : null;
            notification.DeletedBy = definition.IsDeleted ? adminMemberId : null;
            notification.CreatedAt = definition.ScheduledAt.AddHours(-1);
            notification.CreatedBy = adminMemberId;
            if (isNew)
            {
                context.Notifications.Add(notification);
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedPointTransactionsAsync(
        AppDbContext context,
        int adminMemberId,
        DateTime now)
    {
        var members = await context.Members
            .Where(member => member.Email == "demo.foodie01@example.com" ||
                             member.Email == "demo.foodie02@example.com")
            .ToDictionaryAsync(member => member.Email);
        var frames = await context.AvatarFrames.OrderBy(frame => frame.SortOrder).Take(2).ToListAsync();
        var definitions = new[]
        {
            new PointSeed("demo.foodie01@example.com", 200, 1080, "Earn", null, "夏季美食募集活動獎勵", null, now.AddDays(-6)),
            new PointSeed("demo.foodie01@example.com", -frames[1].PointsPrice, 980, "Redeem", frames[1].FrameID, "兌換城市星夜外框", null, now.AddDays(-5)),
            new PointSeed("demo.foodie01@example.com", -100, 880, "AdminAdjust", null, "重複發放活動點數沖銷", adminMemberId, now.AddDays(-4)),
            new PointSeed("demo.foodie02@example.com", 100, 530, "Earn", null, "優質評論獎勵", null, now.AddDays(-6)),
            new PointSeed("demo.foodie02@example.com", -frames[0].PointsPrice, 480, "Redeem", frames[0].FrameID, "兌換晨光暖橙外框", null, now.AddDays(-5)),
            new PointSeed("demo.foodie02@example.com", -50, 430, "AdminAdjust", null, "客服更正點數紀錄", adminMemberId, now.AddDays(-4))
        };

        foreach (var definition in definitions)
        {
            var member = members[definition.MemberEmail];
            if (!await context.PointsTransactions.AnyAsync(item => item.Note == definition.Note))
            {
                context.PointsTransactions.Add(new PointsTransaction
                {
                    MemberID = member.MemberID,
                    Amount = definition.Amount,
                    BalanceAfter = definition.BalanceAfter,
                    Type = definition.Type,
                    RelatedFrameID = definition.RelatedFrameId,
                    Note = definition.Note,
                    CreatedBy = definition.CreatedBy,
                    CreatedAt = definition.CreatedAt
                });
            }

            if (definition.Type == "Redeem" && definition.RelatedFrameId.HasValue)
            {
                if (!await context.MemberAvatarFrames.AnyAsync(item =>
                        item.MemberID == member.MemberID &&
                        item.FrameID == definition.RelatedFrameId.Value))
                {
                    context.MemberAvatarFrames.Add(new MemberAvatarFrame
                    {
                        MemberID = member.MemberID,
                        FrameID = definition.RelatedFrameId.Value,
                        RedeemedAt = definition.CreatedAt
                    });
                }

                member.EquippedFrameID = definition.RelatedFrameId;
            }
        }

        members["demo.foodie01@example.com"].Points = 880;
        members["demo.foodie02@example.com"].Points = 430;
        await context.SaveChangesAsync();
    }

    private static async Task<Image> UpsertImageAsync(
        AppDbContext context,
        int uploadedByMemberId,
        string url,
        string type,
        int sortOrder,
        DateTime now)
    {
        var image = await context.Images.SingleOrDefaultAsync(item => item.ImageURL == url);
        if (image is null)
        {
            image = new Image { ImageURL = url };
            context.Images.Add(image);
        }

        image.UploadedByMemberID = uploadedByMemberId;
        image.ImageType = type;
        image.SortOrder = sortOrder;
        image.UploadedAt = now.AddDays(-7);
        image.IsDeleted = false;
        image.DeletedAt = null;
        image.DeletedBy = null;
        await context.SaveChangesAsync();
        return image;
    }

    private static async Task AddRestaurantImageLinkAsync(
        AppDbContext context,
        int restaurantId,
        int imageId)
    {
        if (!await context.RestaurantImages.AnyAsync(link =>
                link.RestaurantID == restaurantId && link.ImageID == imageId))
        {
            context.RestaurantImages.Add(new RestaurantImage
            {
                RestaurantID = restaurantId,
                ImageID = imageId
            });
            await context.SaveChangesAsync();
        }
    }

    private static async Task RecalculateRestaurantStatsAsync(AppDbContext context)
    {
        var restaurants = await context.Restaurants.ToListAsync();
        foreach (var restaurant in restaurants)
        {
            var ratings = await context.Reviews
                .Where(review => review.RestaurantID == restaurant.RestaurantID &&
                                 !review.IsDeleted &&
                                 review.Status == "Active")
                .Select(review => review.Rating)
                .ToListAsync();
            restaurant.ReviewCount = ratings.Count;
            restaurant.AverageRating = ratings.Count == 0
                ? 0m
                : Math.Round((decimal)ratings.Average(), 2);
        }

        await context.SaveChangesAsync();
    }

    private static void AddBusinessHours(
        AppDbContext context,
        int restaurantId,
        bool splitSaturday)
    {
        for (var day = 1; day <= 7; day++)
        {
            var isClosed = day == 7;
            context.BusinessHours.Add(new BusinessHour
            {
                RestaurantID = restaurantId,
                DayOfWeek = day,
                OpenTime = isClosed ? TimeOnly.MinValue : new TimeOnly(11, 0),
                CloseTime = isClosed ? TimeOnly.MinValue : new TimeOnly(splitSaturday && day == 6 ? 15 : 21, 0),
                IsClosed = isClosed
            });

            if (splitSaturday && day == 6)
            {
                context.BusinessHours.Add(new BusinessHour
                {
                    RestaurantID = restaurantId,
                    DayOfWeek = day,
                    OpenTime = new TimeOnly(17, 0),
                    CloseTime = new TimeOnly(23, 0),
                    IsClosed = false
                });
            }
        }
    }

    private sealed record MemberSeed(
        string UserName,
        string NickName,
        string Email,
        string Role,
        string Status,
        int LevelIndex,
        int Experience,
        int Points);

    private sealed record TagSeed(string Name, bool IsDeleted);

    private sealed record RestaurantSeed(
        string Name,
        string City,
        string District,
        string Address,
        string Phone,
        decimal Latitude,
        decimal Longitude,
        bool IsDeleted,
        string? DeleteReason,
        string OwnerEmail,
        string[] Tags);

    private sealed record ReviewSeed(
        string Content,
        string MemberEmail,
        string RestaurantName,
        int Rating,
        string Status,
        bool IsDeleted);

    private sealed record FolderSeed(string MemberEmail, string Name, bool IsDeleted);

    private sealed record NotificationSeed(
        string Title,
        string Content,
        string Type,
        string? MemberEmail,
        string? TargetRole,
        string? TargetStatus,
        int? TargetLevelId,
        bool IsSent,
        bool IsDeleted,
        DateTime ScheduledAt);

    private sealed record PointSeed(
        string MemberEmail,
        int Amount,
        int BalanceAfter,
        string Type,
        int? RelatedFrameId,
        string Note,
        int? CreatedBy,
        DateTime CreatedAt);
}
