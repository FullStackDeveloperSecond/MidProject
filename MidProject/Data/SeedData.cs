using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Services;

namespace MidProject.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await context.Members.AnyAsync())
        {
            return;
        }

        var now = DateTime.Now;

        var levels = new List<UserLevel>
        {
            new() { LevelName = "新食客", MinExp = 0, Rewards = "基本會員權益" },
            new() { LevelName = "尋味人", MinExp = 500, Rewards = "評論徽章" },
            new() { LevelName = "品味家", MinExp = 1300, Rewards = "達人標章" },
            new() { LevelName = "老饕客", MinExp = 3400, Rewards = "專屬活動邀請" },
            new() { LevelName = "鑑味師", MinExp = 8800, Rewards = "VIP 標章" },
            new() { LevelName = "食之神", MinExp = 23000, Rewards = "尊爵頭銜" }
        };
        context.UserLevels.AddRange(levels);
        await context.SaveChangesAsync();

        var admin = new Member
        {
            UserName = "admin",
            NickName = "系統管理員",
            Email = "admin@example.com",
            PasswordHash = PasswordHashService.HashPassword("Admin123!"),
            Role = "Admin",
            Status = "Normal",
            LevelID = levels[2].LevelID,
            Experience = 2000,
            Points = 500,
            CreatedAt = now.AddMonths(-6),
            UpdatedAt = now.AddDays(-1)
        };

        var members = new List<Member>
        {
            admin,
            CreateMember("Aiden_42", "怡安", "aiden@example.com", "Normal", levels[1].LevelID, 620, 120, now.AddDays(-20)),
            CreateMember("Mason17", "小森", "mason@example.com", "Normal", levels[0].LevelID, 280, 80, now.AddDays(-15)),
            CreateMember("Ethan.85", "亦辰", "ethan@example.com", "Warning", levels[0].LevelID, 180, 30, now.AddDays(-10), "評論用語需注意", warningCount: 1),
            CreateMember("Logan_23", "洛根", "logan@example.com", "Muted", levels[1].LevelID, 760, 150, now.AddDays(-40), "多次發送廣告留言", now.AddDays(7)),
            CreateMember("Caleb76", "凱勒", "caleb@example.com", "Suspended", levels[0].LevelID, 100, 10, now.AddDays(-30), "疑似惡意檢舉，永久停權"),
            CreateMember("Dylan09", "迪倫", "dylan@example.com", "Normal", levels[2].LevelID, 1680, 310, now.AddDays(-90)),
            CreateMember("Wyatt63", "懷特", "wyatt@example.com", "Normal", levels[1].LevelID, 840, 220, now.AddDays(-55)),
            CreateMember("Gavin28", "加文", "gavin@example.com", "Deleted", levels[0].LevelID, 20, 0, now.AddDays(-120), "會員自行申請刪除", null, true, now.AddDays(-5), null)
        };
        context.Members.AddRange(members);
        await context.SaveChangesAsync();

        var tags = new[]
        {
            "台式料理", "日式料理", "韓式料理", "義式料理", "泰式料理", "越南料理",
            "火鍋", "燒烤", "咖啡廳", "甜點", "早午餐", "素食"
        }.Select(name => new Tag { TagName = name }).ToList();
        context.Tags.AddRange(tags);
        await context.SaveChangesAsync();

        var restaurants = new List<Restaurant>
        {
            CreateRestaurant("木柵早午餐", "台北市", "文山區", "木柵路一段 10 號", "02-2939-5510", "週末人潮較多。", admin.MemberID, 25.000001m, 121.580001m, now.AddDays(-35)),
            CreateRestaurant("北投義麵坊", "台北市", "北投區", "光明路 22 號", "02-2891-6620", "義大利麵與燉飯。", admin.MemberID, 25.136000m, 121.502000m, now.AddDays(-32)),
            CreateRestaurant("象山泰香", "台北市", "信義區", "信義路五段 66 號", "02-2722-9008", "熱門泰式料理。", admin.MemberID, 25.033000m, 121.571000m, now.AddDays(-30)),
            CreateRestaurant("河岸越南粉", "台北市", "萬華區", "環河南路二段 88 號", "02-2388-7102", "湯頭清爽。", admin.MemberID, 25.036000m, 121.499000m, now.AddDays(-28)),
            CreateRestaurant("綠意蔬食", "台北市", "中正區", "羅斯福路一段 18 號", "02-2399-7788", "素食友善。", admin.MemberID, null, null, now.AddDays(-25)),
            CreateRestaurant("晴光甜點室", "台北市", "中山區", "雙城街 12 號", "02-2599-2300", "招牌檸檬塔。", admin.MemberID, null, null, now.AddDays(-22)),
            CreateRestaurant("民生燒肉舖", "台北市", "松山區", "民生東路五段 99 號", "02-2766-4399", "多人聚餐適合。", admin.MemberID, null, null, now.AddDays(-18)),
            CreateRestaurant("松山韓味屋", "台北市", "松山區", "八德路四段 123 號", "02-2577-8801", "小菜可續。", admin.MemberID, null, null, now.AddDays(-12)),
            CreateRestaurant("東區義式廚房", "台北市", "大安區", "忠孝東路四段 200 號", "02-2777-0001", "停用展示資料。", admin.MemberID, null, null, now.AddDays(-70), true, now.AddDays(-3), admin.MemberID, "違規內容"),
            CreateRestaurant("老王牛肉麵", "台北市", "中正區", "重慶南路一段 5 號", "02-2311-8899", "停用展示資料。", admin.MemberID, null, null, now.AddDays(-80), true, now.AddDays(-12), admin.MemberID, "已歇業")
        };
        context.Restaurants.AddRange(restaurants);
        await context.SaveChangesAsync();

        AddRestaurantTags(context, restaurants[0], tags, "早午餐", "咖啡廳");
        AddRestaurantTags(context, restaurants[1], tags, "義式料理");
        AddRestaurantTags(context, restaurants[2], tags, "泰式料理");
        AddRestaurantTags(context, restaurants[3], tags, "越南料理");
        AddRestaurantTags(context, restaurants[4], tags, "素食", "台式料理");
        AddRestaurantTags(context, restaurants[5], tags, "甜點", "咖啡廳");
        AddRestaurantTags(context, restaurants[6], tags, "燒烤");
        AddRestaurantTags(context, restaurants[7], tags, "韓式料理");
        AddRestaurantTags(context, restaurants[8], tags, "義式料理");
        AddRestaurantTags(context, restaurants[9], tags, "台式料理");
        await context.SaveChangesAsync();

        foreach (var restaurant in restaurants)
        {
            AddBusinessHours(context, restaurant.RestaurantID);
        }
        await context.SaveChangesAsync();

        var images = new List<Image>();
        images.AddRange(restaurants.Select((restaurant, index) => new Image
        {
            UploadedByMemberID = admin.MemberID,
            ImageURL = $"/uploads/RestaurantCover/restaurant-cover-{index + 1:00}.jpg",
            ImageType = "RestaurantCover",
            SortOrder = 0,
            UploadedAt = now.AddDays(-10 + index)
        }));
        images.AddRange(new[]
        {
            CreateImage(admin.MemberID, "/uploads/RestaurantEnvironment/restaurant-env-01.jpg", "RestaurantEnvironment", 1, now),
            CreateImage(admin.MemberID, "/uploads/RestaurantEnvironment/restaurant-env-02.jpg", "RestaurantEnvironment", 2, now),
            CreateImage(admin.MemberID, "/uploads/RestaurantEnvironment/restaurant-env-03.jpg", "RestaurantEnvironment", 1, now),
            CreateImage(admin.MemberID, "/uploads/ReviewImage/review-01.jpg", "ReviewImage", 0, now),
            CreateImage(admin.MemberID, "/uploads/ReviewImage/review-02.jpg", "ReviewImage", 0, now),
            CreateImage(admin.MemberID, "/uploads/ReviewImage/review-03.jpg", "ReviewImage", 0, now),
            CreateImage(admin.MemberID, "/uploads/MemberAvatar/avatar-admin.jpg", "MemberAvatar", 0, now),
            CreateImage(admin.MemberID, "/uploads/MemberAvatar/avatar-aiden.jpg", "MemberAvatar", 0, now),
            CreateImage(admin.MemberID, "/uploads/MemberAvatar/avatar-mason.jpg", "MemberAvatar", 0, now)
        });
        context.Images.AddRange(images);
        await context.SaveChangesAsync();

        admin.AvatarImageID = images[^3].ImageID;
        members[1].AvatarImageID = images[^2].ImageID;
        members[2].AvatarImageID = images[^1].ImageID;
        await context.SaveChangesAsync();

        var coverImages = images.Where(i => i.ImageType == "RestaurantCover").ToList();
        for (int i = 0; i < restaurants.Count; i++)
        {
            context.RestaurantImages.Add(new RestaurantImage { RestaurantID = restaurants[i].RestaurantID, ImageID = coverImages[i].ImageID });
        }
        context.RestaurantImages.Add(new RestaurantImage { RestaurantID = restaurants[0].RestaurantID, ImageID = images.First(i => i.ImageURL.Contains("restaurant-env-01")).ImageID });
        context.RestaurantImages.Add(new RestaurantImage { RestaurantID = restaurants[0].RestaurantID, ImageID = images.First(i => i.ImageURL.Contains("restaurant-env-02")).ImageID });
        context.RestaurantImages.Add(new RestaurantImage { RestaurantID = restaurants[5].RestaurantID, ImageID = images.First(i => i.ImageURL.Contains("restaurant-env-03")).ImageID });
        await context.SaveChangesAsync();

        var reviews = new List<Review>
        {
            CreateReview(members[1], restaurants[0], 5, "餐點份量足，適合週末聚餐。", "Active", now.AddDays(-9)),
            CreateReview(members[2], restaurants[0], 4, "服務速度普通，但餐點味道不錯。", "Active", now.AddDays(-8)),
            CreateReview(members[3], restaurants[1], 3, "環境乾淨，座位稍微擁擠。", "PendingReview", now.AddDays(-7), 1),
            CreateReview(members[4], restaurants[2], 2, "老闆態度很差，需要改善服務。", "PendingReview", now.AddDays(-6), 2),
            CreateReview(members[5], restaurants[3], 1, "想知道優惠可以加我的 LINE：food888。", "PendingReview", now.AddDays(-5), 2),
            CreateReview(members[6], restaurants[4], 5, "蔬食選擇很多，口味清爽。", "Active", now.AddDays(-4)),
            CreateReview(members[7], restaurants[5], 5, "甜點很精緻，適合拍照。", "Active", now.AddDays(-3)),
            CreateReview(members[1], restaurants[6], 4, "燒肉品質穩定。", "Active", now.AddDays(-2)),
            CreateReview(members[2], restaurants[7], 4, "韓式小菜不錯。", "Active", now.AddDays(-1)),
            CreateReview(members[3], restaurants[1], 2, "照片看起來跟實際餐點差很多。", "Active", now.AddDays(-20), 1, true, now.AddDays(-2), admin.MemberID)
        };
        context.Reviews.AddRange(reviews);
        await context.SaveChangesAsync();

        var reviewImageIds = images.Where(i => i.ImageType == "ReviewImage").Select(i => i.ImageID).ToList();
        context.ReviewImages.AddRange(
            new ReviewImage { ReviewID = reviews[0].ReviewID, ImageID = reviewImageIds[0] },
            new ReviewImage { ReviewID = reviews[3].ReviewID, ImageID = reviewImageIds[1] },
            new ReviewImage { ReviewID = reviews[4].ReviewID, ImageID = reviewImageIds[2] }
        );
        await context.SaveChangesAsync();

        RecalculateRestaurantStats(restaurants, reviews);
        await context.SaveChangesAsync();

        var folders = new List<FavoriteFolder>
        {
            new() { MemberID = members[1].MemberID, FolderName = "週末想吃", CreatedAt = now.AddDays(-18), UpdatedAt = now.AddDays(-2) },
            new() { MemberID = members[2].MemberID, FolderName = "甜點清單", CreatedAt = now.AddDays(-16), UpdatedAt = now.AddDays(-1) },
            new() { MemberID = members[6].MemberID, FolderName = "聚餐備選", CreatedAt = now.AddDays(-12), UpdatedAt = now.AddDays(-1) }
        };
        context.FavoriteFolders.AddRange(folders);
        await context.SaveChangesAsync();

        context.Favorites.AddRange(
            new Favorite { MemberID = members[1].MemberID, RestaurantID = restaurants[0].RestaurantID, FavoriteFolderID = folders[0].FavoriteFolderID, CreatedAt = now.AddDays(-5), UpdatedAt = now.AddDays(-5) },
            new Favorite { MemberID = members[1].MemberID, RestaurantID = restaurants[2].RestaurantID, FavoriteFolderID = folders[0].FavoriteFolderID, CreatedAt = now.AddDays(-4), UpdatedAt = now.AddDays(-4) },
            new Favorite { MemberID = members[2].MemberID, RestaurantID = restaurants[5].RestaurantID, FavoriteFolderID = folders[1].FavoriteFolderID, CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-3) },
            new Favorite { MemberID = members[6].MemberID, RestaurantID = restaurants[6].RestaurantID, FavoriteFolderID = folders[2].FavoriteFolderID, CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-2) }
        );
        await context.SaveChangesAsync();

        var reports = new List<Report>
        {
            CreateReport(members[1].MemberID, reviewId: reviews[3].ReviewID, reason: "使用不雅言論", status: "Pending", createdAt: now.AddHours(-6)),
            CreateReport(members[2].MemberID, reviewId: reviews[4].ReviewID, reason: "評論含有廣告內容", status: "Pending", createdAt: now.AddHours(-5)),
            CreateReport(members[6].MemberID, restaurantId: restaurants[8].RestaurantID, reason: "餐廳資訊與實際地址不符，疑似假店家。", status: "Pending", createdAt: now.AddHours(-4)),
            CreateReport(members[7].MemberID, imageId: images.First(i => i.ImageURL.Contains("restaurant-env-01")).ImageID, reason: "圖片與餐廳實際環境不符，疑似盜用網路照片。", status: "Approved", createdAt: now.AddDays(-8), handledAt: now.AddDays(-7), handledBy: admin.MemberID, adminNote: "檢舉成立，已通知上傳者。"),
            CreateReport(members[3].MemberID, reportedMemberId: members[5].MemberID, reason: "多次發布無關內容。", status: "Approved", createdAt: now.AddDays(-6), handledAt: now.AddDays(-5), handledBy: admin.MemberID, adminNote: "檢舉成立，已列入會員狀態處理參考。"),
            CreateReport(members[1].MemberID, reportedMemberId: members[4].MemberID, reason: "多次發布無關廣告內容。", status: "Approved", createdAt: now.AddDays(-20), handledAt: now.AddDays(-19), handledBy: admin.MemberID, adminNote: "檢舉成立，第一次廣告留言警告。"),
            CreateReport(members[2].MemberID, reportedMemberId: members[4].MemberID, reason: "重複張貼推銷連結，疑似機器人帳號。", status: "Approved", createdAt: now.AddDays(-10), handledAt: now.AddDays(-9), handledBy: admin.MemberID, adminNote: "檢舉成立，累積次數已達禁言門檻。"),
            CreateReport(members[7].MemberID, reportedMemberId: members[4].MemberID, reason: "留言內容與討論主題無關，疑似洗版。", status: "Approved", createdAt: now.AddDays(-3), handledAt: now.AddDays(-2), handledBy: admin.MemberID, adminNote: "檢舉成立，持續觀察後續行為。"),
            CreateReport(members[4].MemberID, reviewId: reviews[2].ReviewID, reason: "內容不實", status: "Rejected", createdAt: now.AddDays(-5), handledAt: now.AddDays(-4), handledBy: admin.MemberID, adminNote: "查無明確違規，駁回檢舉。"),
            CreateReport(members[2].MemberID, restaurantId: restaurants[1].RestaurantID, reason: "餐廳電話疑似錯誤。", status: "Rejected", createdAt: now.AddDays(-4), handledAt: now.AddDays(-3), handledBy: admin.MemberID, adminNote: "資料已人工確認，維持原資料。")
        };
        context.Reports.AddRange(reports);
        await context.SaveChangesAsync();

        context.Notifications.AddRange(
            new Notification { MemberID = members[1].MemberID, NotificationType = "Personal", Title = "檢舉結果通知", Content = "您提交的檢舉已完成審核。", ScheduledAt = now.AddHours(-1), IsSent = false, CreatedAt = now.AddDays(-1), CreatedBy = admin.MemberID },
            new Notification { MemberID = members[3].MemberID, NotificationType = "Personal", Title = "會員狀態提醒", Content = "請注意評論用語，避免再次違規。", ScheduledAt = now.AddDays(1), IsSent = false, CreatedAt = now, CreatedBy = admin.MemberID },
            new Notification { MemberID = members[2].MemberID, NotificationType = "Personal", Title = "系統通知", Content = "您的資料已更新。", ScheduledAt = now.AddDays(-3), SentAt = now.AddDays(-3).AddMinutes(5), IsSent = true, CreatedAt = now.AddDays(-4), CreatedBy = admin.MemberID },
            new Notification { NotificationType = "Condition", TargetRole = "User", Title = "會員公告", Content = "本週新增多間推薦餐廳。", ScheduledAt = now.AddHours(-2), IsSent = false, CreatedAt = now.AddDays(-1), CreatedBy = admin.MemberID },
            new Notification { NotificationType = "Condition", TargetStatus = "Warning", Title = "狀態提醒", Content = "請留意近期平台規範。", ScheduledAt = now.AddHours(4), IsSent = false, CreatedAt = now, CreatedBy = admin.MemberID },
            new Notification { NotificationType = "Condition", TargetLevelID = levels[1].LevelID, Title = "等級活動", Content = "美食探索者限定活動開跑。", ScheduledAt = now.AddDays(2), IsSent = false, CreatedAt = now, CreatedBy = admin.MemberID },
            new Notification { NotificationType = "Condition", Title = "全站通知", Content = "系統將於今晚進行維護。", ScheduledAt = now.AddHours(-3), IsSent = false, CreatedAt = now.AddDays(-1), CreatedBy = admin.MemberID }
        );
        await context.SaveChangesAsync();
    }

    private static Member CreateMember(string userName, string nickName, string email, string status, int levelId, int experience, int points, DateTime createdAt, string? adminNote = null, DateTime? penaltyEndAt = null, bool isDeleted = false, DateTime? deletedAt = null, int? deletedBy = null, int warningCount = 0)
    {
        return new Member
        {
            UserName = userName,
            NickName = nickName,
            Email = email,
            PasswordHash = PasswordHashService.HashPassword("User123!"),
            Role = "User",
            Status = status,
            LevelID = levelId,
            Experience = experience,
            Points = points,
            WarningCount = warningCount,
            AdminNote = adminNote,
            PenaltyEndAt = penaltyEndAt,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddDays(1)
        };
    }

    private static Restaurant CreateRestaurant(string name, string city, string district, string address, string phone, string? note, int memberId, decimal? latitude, decimal? longitude, DateTime createdAt, bool isDeleted = false, DateTime? deletedAt = null, int? deletedBy = null, string? deleteReason = null)
    {
        return new Restaurant
        {
            Name = name,
            City = city,
            District = district,
            DetailedAddress = address,
            Phone = phone,
            Note = note,
            MemberID = memberId,
            Latitude = latitude,
            Longitude = longitude,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddDays(1),
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy,
            DeleteReason = deleteReason
        };
    }

    private static Image CreateImage(int memberId, string url, string type, int sortOrder, DateTime now)
    {
        return new Image
        {
            UploadedByMemberID = memberId,
            ImageURL = url,
            ImageType = type,
            SortOrder = sortOrder,
            UploadedAt = now.AddDays(-2)
        };
    }

    private static Review CreateReview(Member member, Restaurant restaurant, int rating, string content, string status, DateTime createdAt, int reportCount = 0, bool isDeleted = false, DateTime? deletedAt = null, int? deletedBy = null)
    {
        return new Review
        {
            MemberID = member.MemberID,
            RestaurantID = restaurant.RestaurantID,
            Rating = rating,
            Content = content,
            Status = status,
            ReportCount = reportCount,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddHours(2)
        };
    }

    private static Report CreateReport(int reporterId, int? reportedMemberId = null, int? restaurantId = null, int? reviewId = null, int? imageId = null, string reason = "", string status = "Pending", DateTime? createdAt = null, DateTime? handledAt = null, int? handledBy = null, string? adminNote = null, string? category = null)
    {
        var resolvedCategory = category ?? (reportedMemberId.HasValue ? "會員"
            : reviewId.HasValue ? "評論"
            : restaurantId.HasValue ? "餐廳"
            : imageId.HasValue ? "圖片"
            : "其他");

        return new Report
        {
            ReporterMemberID = reporterId,
            ReportedMemberID = reportedMemberId,
            RestaurantID = restaurantId,
            ReviewID = reviewId,
            ImageID = imageId,
            Reason = reason,
            Category = resolvedCategory,
            Status = status,
            CreatedAt = createdAt ?? DateTime.Now,
            HandledAt = handledAt,
            HandledByMemberID = handledBy,
            AdminNote = adminNote
        };
    }

    private static void AddRestaurantTags(AppDbContext context, Restaurant restaurant, List<Tag> tags, params string[] tagNames)
    {
        foreach (var tagName in tagNames)
        {
            var tag = tags.Single(t => t.TagName == tagName);
            context.RestaurantTags.Add(new RestaurantTag { RestaurantID = restaurant.RestaurantID, TagID = tag.TagID });
        }
    }

    private static void AddBusinessHours(AppDbContext context, int restaurantId)
    {
        context.BusinessHours.AddRange(
            new BusinessHour { RestaurantID = restaurantId, DayOfWeek = 1, OpenTime = new TimeOnly(11, 0), CloseTime = new TimeOnly(14, 0), IsClosed = false },
            new BusinessHour { RestaurantID = restaurantId, DayOfWeek = 1, OpenTime = new TimeOnly(17, 0), CloseTime = new TimeOnly(21, 0), IsClosed = false },
            new BusinessHour { RestaurantID = restaurantId, DayOfWeek = 2, OpenTime = new TimeOnly(11, 0), CloseTime = new TimeOnly(21, 0), IsClosed = false },
            new BusinessHour { RestaurantID = restaurantId, DayOfWeek = 3, OpenTime = new TimeOnly(11, 0), CloseTime = new TimeOnly(21, 0), IsClosed = false },
            new BusinessHour { RestaurantID = restaurantId, DayOfWeek = 4, OpenTime = new TimeOnly(11, 0), CloseTime = new TimeOnly(21, 0), IsClosed = false },
            new BusinessHour { RestaurantID = restaurantId, DayOfWeek = 5, OpenTime = new TimeOnly(11, 0), CloseTime = new TimeOnly(22, 0), IsClosed = false },
            new BusinessHour { RestaurantID = restaurantId, DayOfWeek = 6, OpenTime = new TimeOnly(10, 0), CloseTime = new TimeOnly(22, 0), IsClosed = false },
            new BusinessHour { RestaurantID = restaurantId, DayOfWeek = 7, OpenTime = new TimeOnly(0, 0), CloseTime = new TimeOnly(0, 0), IsClosed = true }
        );
    }

    private static void RecalculateRestaurantStats(IEnumerable<Restaurant> restaurants, IEnumerable<Review> reviews)
    {
        foreach (var restaurant in restaurants)
        {
            var activeReviews = reviews
                .Where(review => review.RestaurantID == restaurant.RestaurantID && !review.IsDeleted && review.Status == "Active")
                .ToList();

            restaurant.ReviewCount = activeReviews.Count;
            restaurant.AverageRating = activeReviews.Count == 0
                ? 0
                : Math.Round((decimal)activeReviews.Average(review => review.Rating), 2);
        }
    }
}
