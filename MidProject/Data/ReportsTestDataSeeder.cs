using Microsoft.EntityFrameworkCore;
using MidProject.Models;

namespace MidProject.Data;

/// <summary>
/// Reports 模組專用的測試資料種子。
/// 這批大量 Dashboard 資料獨立於一般模組化 SeedData，並使用資料庫裡既有的
/// Restaurants / Reviews / Images / Members 建立有效關聯。
/// </summary>
public static class ReportsTestDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 已經有夠多測試資料就不重複塞，避免每次啟動都新增一批
        if (await context.Reports.CountAsync() >= 60)
        {
            return;
        }

        var members = await context.Members.Where(m => !m.IsDeleted).OrderBy(m => m.MemberID).ToListAsync();
        var restaurants = await context.Restaurants.Where(r => !r.IsDeleted).OrderBy(r => r.RestaurantID).ToListAsync();
        var reviews = await context.Reviews.Where(r => !r.IsDeleted).OrderBy(r => r.ReviewID).ToListAsync();
        var images = await context.Images.Where(i => !i.IsDeleted).OrderBy(i => i.ImageID).ToListAsync();

        if (members.Count < 2 || restaurants.Count == 0 || reviews.Count == 0 || images.Count == 0)
        {
            // 基礎資料（Members/Restaurants/Reviews/Images）還沒建立好，無法產生有效的檢舉關聯，先跳過
            return;
        }

        var admin = members.FirstOrDefault(m => m.Role == "Admin") ?? members[0];
        var reporters = members.Where(m => m.MemberID != admin.MemberID).ToList();
        if (reporters.Count == 0) reporters = members;

        var now = DateTime.Now;

        var categories = new[] { "不實資訊", "廣告洗版", "人身攻擊", "仇恨言論", "色情內容", "垃圾訊息" };
        var statuses = new[] { "Pending", "Approved", "Rejected" };

        // 每個分類對應一句比較貼近情境的檢舉原因，讓測試資料看起來更真實
        var reasonsByCategory = new Dictionary<string, string[]>
        {
            ["不實資訊"] = new[] { "餐廳資訊與實際狀況不符。", "內容疑似造假，與現場不符。" },
            ["廣告洗版"] = new[] { "留言內容為廣告推銷，非真實評論。", "重複張貼推廣連結。" },
            ["人身攻擊"] = new[] { "內容包含針對他人的辱罵字眼。", "評論用語過於情緒化並人身攻擊店家。" },
            ["仇恨言論"] = new[] { "內容含有歧視性言論。", "留言帶有攻擊特定族群的字眼。" },
            ["色情內容"] = new[] { "圖片內容不當，疑似色情。", "上傳內容與美食主題無關，涉及不當畫面。" },
            ["垃圾訊息"] = new[] { "重複張貼相同內容洗版。", "內容與主題無關，疑似機器人發送。" },
        };

        var reports = new List<Report>();
        var random = new Random(42);

        // 近 12 個月（含當月）每個月都產生 5~10 筆檢舉資料，讓 Dashboard 的每月統計圖表每個月都有資料可顯示
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var earliestMonth = monthStart.AddMonths(-11);

        var i = 0;
        for (var m = 0; m < 12; m++)
        {
            var bucketMonth = earliestMonth.AddMonths(m);
            var daysInMonth = DateTime.DaysInMonth(bucketMonth.Year, bucketMonth.Month);
            var countThisMonth = random.Next(5, 11); // 5~10 筆

            for (var n = 0; n < countThisMonth; n++)
            {
                var category = categories[i % categories.Length];
                var status = statuses[i % statuses.Length];
                var reporter = reporters[i % reporters.Count];
                var reasonOptions = reasonsByCategory[category];
                var reason = reasonOptions[i % reasonOptions.Length];

                var day = random.Next(1, daysInMonth + 1);
                var createdAt = new DateTime(bucketMonth.Year, bucketMonth.Month, day, random.Next(0, 24), random.Next(0, 60), 0);
                if (createdAt > now) createdAt = now; // 避免當月產生未來時間

                // 目標類型隨機分配（跟 i % 3 脫鉤，避免跟 status 的分配同步導致每種狀態都只對應到單一目標類型）
                int? restaurantId = null, reviewId = null, imageId = null;
                switch (random.Next(3))
                {
                    case 0:
                        restaurantId = restaurants[i % restaurants.Count].RestaurantID;
                        break;
                    case 1:
                        reviewId = reviews[i % reviews.Count].ReviewID;
                        break;
                    case 2:
                        imageId = images[i % images.Count].ImageID;
                        break;
                }

                var report = new Report
                {
                    ReporterMemberID = reporter.MemberID,
                    RestaurantID = restaurantId,
                    ReviewID = reviewId,
                    ImageID = imageId,
                    Reason = reason,
                    Category = category,
                    Status = status,
                    CreatedAt = createdAt
                };

                // 已處理（Approved/Rejected）的檢舉補上處理時間、處理人、備註
                if (status != "Pending")
                {
                    report.HandledAt = createdAt.AddDays(1) > now ? now : createdAt.AddDays(1);
                    report.HandledByMemberID = admin.MemberID;
                    report.AdminNote = status == "Approved"
                        ? "已確認違規，檢舉成立。"
                        : "查無明確違規事證，駁回檢舉。";
                }

                reports.Add(report);
                i++;
            }
        }

        context.Reports.AddRange(reports);
        await context.SaveChangesAsync();
    }
}
