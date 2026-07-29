using System.Data;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models.DTOs;
using MidProject.Models;
using MidProject.Services;

namespace MidProject.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly AppDbContext _context;

    public ReportRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<Report>> GetReportsAsync(ReportQueryParams query, DateTime today)
    {
        var q = _context.Reports
            .Include(r => r.ReporterMember)
            .Include(r => r.Restaurant).ThenInclude(rest => rest!.Member)
            .Include(r => r.Review).ThenInclude(rev => rev!.Member)
            .Include(r => r.Image).ThenInclude(img => img!.UploadedByMember)
            .Include(r => r.HandledByMember)
            .Where(r => !r.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(r => r.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Category))
            q = q.Where(r => r.Category == query.Category);

        if (query.RestaurantID.HasValue)
            q = q.Where(r => r.RestaurantID == query.RestaurantID.Value);

        if (query.ReviewID.HasValue)
            q = q.Where(r => r.ReviewID == query.ReviewID.Value);

        // 依檢舉目標類型篩選（使用者只能針對餐廳/評論/圖片檢舉，不含會員）
        q = query.TargetType switch
        {
            "Restaurant" => q.Where(r => r.RestaurantID != null),
            "Review" => q.Where(r => r.ReviewID != null),
            "Image" => q.Where(r => r.ImageID != null),
            _ => q
        };

        if (query.DateFrom.HasValue)
            q = q.Where(r => r.CreatedAt >= query.DateFrom.Value.Date);

        if (query.DateTo.HasValue)
            q = q.Where(r => r.CreatedAt < query.DateTo.Value.Date.AddDays(1));

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();

            // 關鍵字模糊比對中文的狀態/目標名稱：只要標籤「包含」關鍵字就算符合（例如打「駁」就能比對到「駁回檢舉」）
            var matchesPendingStatus = "待處理".Contains(keyword);
            var matchesApprovedStatus = "檢舉成立".Contains(keyword);
            var matchesRejectedStatus = "駁回檢舉".Contains(keyword);
            var matchesRestaurantTarget = "餐廳".Contains(keyword);
            var matchesReviewTarget = "評論".Contains(keyword);
            var matchesImageTarget = "圖片".Contains(keyword);

            q = q.Where(r =>
                r.Reason.Contains(keyword) ||
                (r.ReporterMember != null && r.ReporterMember.UserName.Contains(keyword)) ||
                (r.Restaurant != null && r.Restaurant.Member != null && r.Restaurant.Member.UserName.Contains(keyword)) ||
                (r.Review != null && r.Review.Member != null && r.Review.Member.UserName.Contains(keyword)) ||
                (r.Image != null && r.Image.UploadedByMember != null && r.Image.UploadedByMember.UserName.Contains(keyword)) ||
                (r.Restaurant != null && r.Restaurant.Name.Contains(keyword)) ||
                (r.Category != null && r.Category.Contains(keyword)) ||
                (matchesPendingStatus && r.Status == "Pending") ||
                (matchesApprovedStatus && r.Status == "Approved") ||
                (matchesRejectedStatus && r.Status == "Rejected") ||
                (matchesRestaurantTarget && r.RestaurantID != null) ||
                (matchesReviewTarget && r.ReviewID != null) ||
                (matchesImageTarget && r.ImageID != null));
        }

        var totalCount = await q.CountAsync();

        // 處理天數：已處理＝處理日－檢舉日；待處理＝今天－檢舉日，兩者皆為非負的經過天數。
        // 與 ReportDto.ProcessingDays 的計算一致，在資料庫端算出來才能正確排序＋分頁。
        var currentDate = today.Date;

        // 依指定欄位排序，預設依檢舉日期新到舊
        q = (query.SortBy, query.SortDirection?.ToLower()) switch
        {
            ("ReportID", "asc") => q.OrderBy(r => r.ReportID),
            ("ReportID", "desc") => q.OrderByDescending(r => r.ReportID),
            ("Status", "asc") => q.OrderBy(r => r.Status),
            ("Status", "desc") => q.OrderByDescending(r => r.Status),
            ("ProcessingDays", "asc") => q.OrderBy(r => r.Status == "Pending"
                ? EF.Functions.DateDiffDay(r.CreatedAt.Date, currentDate)
                : (r.HandledAt.HasValue ? EF.Functions.DateDiffDay(r.CreatedAt.Date, r.HandledAt.Value.Date) : 0)),
            ("ProcessingDays", "desc") => q.OrderByDescending(r => r.Status == "Pending"
                ? EF.Functions.DateDiffDay(r.CreatedAt.Date, currentDate)
                : (r.HandledAt.HasValue ? EF.Functions.DateDiffDay(r.CreatedAt.Date, r.HandledAt.Value.Date) : 0)),
            ("CreatedAt", "asc") => q.OrderBy(r => r.CreatedAt),
            _ => q.OrderByDescending(r => r.CreatedAt)
        };

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 10 : query.PageSize;

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Report>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Report?> GetByIdAsync(int reportId)
    {
        // AsNoTracking：處理檢舉改用 ExecuteUpdate 原子更新後，需重新從資料庫讀取最新狀態，
        // 不能拿到 EF 身分對應快取中的舊實體（否則會誤判仍為待處理）
        return await _context.Reports
            .AsNoTracking()
            .Include(r => r.ReporterMember)
            .Include(r => r.Restaurant).ThenInclude(rest => rest!.Member)
            .Include(r => r.Review).ThenInclude(rev => rev!.Member)
            .Include(r => r.Review).ThenInclude(rev => rev!.Restaurant)
            .Include(r => r.Image).ThenInclude(img => img!.UploadedByMember)
            // 圖片本身沒有直接的餐廳外鍵，要透過「餐廳照片」或「評論照片」關聯回推所屬餐廳，方便詳情頁顯示
            .Include(r => r.Image).ThenInclude(img => img!.RestaurantImages).ThenInclude(ri => ri.Restaurant)
            .Include(r => r.Image).ThenInclude(img => img!.ReviewImages).ThenInclude(rvi => rvi.Review).ThenInclude(rv => rv!.Restaurant)
            .Include(r => r.HandledByMember)
            .FirstOrDefaultAsync(r => r.ReportID == reportId && !r.IsDeleted);
    }

    public async Task AddAsync(Report report)
    {
        await _context.Reports.AddAsync(report);
    }

    public async Task<int?> GetTargetOwnerMemberIdAsync(int? restaurantId, int? reviewId, int? imageId)
    {
        int? ownerId = null;
        if (restaurantId.HasValue)
            ownerId = await _context.Restaurants.Where(r => r.RestaurantID == restaurantId).Select(r => (int?)r.MemberID).FirstOrDefaultAsync();
        else if (reviewId.HasValue)
            ownerId = await _context.Reviews.Where(r => r.ReviewID == reviewId).Select(r => (int?)r.MemberID).FirstOrDefaultAsync();
        else if (imageId.HasValue)
            ownerId = await _context.Images.Where(i => i.ImageID == imageId).Select(i => i.UploadedByMemberID).FirstOrDefaultAsync();

        if (ownerId == null) return null;

        // 排除 Admin：管理員不列為被檢舉會員（與 HandleReportAsync 一致）
        var isAdmin = await _context.Members.AnyAsync(m => m.MemberID == ownerId && m.Role == "Admin");
        return isAdmin ? null : ownerId;
    }

    public async Task<int> TryHandleAsync(int reportId, string status, string category, string adminNote, int? reportedMemberId, int adminMemberId, DateTime handledAt)
    {
        // 定案、違規內容軟刪除、餐廳統計重算及會員累積懲處必須一起成功或一起回滾。
        // Serializable 同時保護同一會員多筆檢舉並行成立時的累積次數計算。
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            // 條件式原子更新：WHERE Status='Pending'，資料庫層保證只有一人能把待處理改為已處理。
            var affected = await _context.Reports
                .Where(r => r.ReportID == reportId && r.Status == "Pending" && !r.IsDeleted)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, status)
                    .SetProperty(r => r.Category, category)
                    .SetProperty(r => r.AdminNote, adminNote)
                    .SetProperty(r => r.ReportedMemberID, reportedMemberId)
                    .SetProperty(r => r.HandledByMemberID, adminMemberId)
                    .SetProperty(r => r.HandledAt, handledAt));

            if (affected == 0)
            {
                await transaction.RollbackAsync();
                return 0;
            }

            if (status == "Approved")
            {
                await ApplyApprovedConsequencesAsync(reportId, reportedMemberId, adminMemberId, handledAt);
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return affected;
        }
        catch
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task ApplyApprovedConsequencesAsync(
        int reportId,
        int? reportedMemberId,
        int adminMemberId,
        DateTime handledAt)
    {
        var target = await _context.Reports
            .AsNoTracking()
            .Where(report => report.ReportID == reportId)
            .Select(report => new
            {
                report.RestaurantID,
                report.ReviewID,
                report.ImageID,
                report.Category
            })
            .SingleAsync();

        if (target.RestaurantID.HasValue)
        {
            var restaurant = await _context.Restaurants
                .FirstOrDefaultAsync(item => item.RestaurantID == target.RestaurantID.Value);
            if (restaurant is { IsDeleted: false })
            {
                restaurant.IsDeleted = true;
                restaurant.DeletedAt = handledAt;
                restaurant.DeletedBy = adminMemberId;
                restaurant.DeleteReason = BuildReportDeleteReason(reportId, target.Category);
                restaurant.UpdatedAt = handledAt;
            }
        }
        else if (target.ReviewID.HasValue)
        {
            var review = await _context.Reviews
                .FirstOrDefaultAsync(item => item.ReviewID == target.ReviewID.Value);
            if (review is { IsDeleted: false })
            {
                review.IsDeleted = true;
                review.DeletedAt = handledAt;
                review.DeletedBy = adminMemberId;
                review.UpdatedAt = handledAt;

                var stats = await _context.Reviews
                    .Where(item =>
                        item.RestaurantID == review.RestaurantID &&
                        item.ReviewID != review.ReviewID &&
                        !item.IsDeleted &&
                        item.Status == "Active")
                    .GroupBy(_ => 1)
                    .Select(group => new
                    {
                        Count = group.Count(),
                        Average = group.Average(item => (decimal)item.Rating)
                    })
                    .FirstOrDefaultAsync();
                var restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(item => item.RestaurantID == review.RestaurantID);
                if (restaurant != null)
                {
                    restaurant.ReviewCount = stats?.Count ?? 0;
                    restaurant.AverageRating = stats?.Average ?? 0;
                    restaurant.UpdatedAt = handledAt;
                }
            }
        }
        else if (target.ImageID.HasValue)
        {
            var image = await _context.Images
                .FirstOrDefaultAsync(item => item.ImageID == target.ImageID.Value);
            if (image is { IsDeleted: false })
            {
                image.IsDeleted = true;
                image.DeletedAt = handledAt;
                image.DeletedBy = adminMemberId;
            }
        }

        if (!reportedMemberId.HasValue)
        {
            return;
        }

        var member = await _context.Members
            .FirstOrDefaultAsync(item =>
                item.MemberID == reportedMemberId.Value &&
                item.Role != "Admin");
        if (member == null)
        {
            return;
        }

        var approvedCount = await _context.Reports.CountAsync(report =>
            report.ReportedMemberID == reportedMemberId.Value &&
            report.Status == "Approved" &&
            !report.IsDeleted);
        MemberPenaltyPolicy.Apply(member, approvedCount, handledAt, adminMemberId);
    }

    private static string BuildReportDeleteReason(int reportId, string category) =>
        $"檢舉成立（案件 #{reportId}，分類：{category}）";

    public async Task<List<Notification>> GetNotificationsByReportAsync(int reportId)
    {
        // 撈該檢舉的通知紀錄（含尚未發送的），供詳情頁「通知紀錄」顯示；
        // 通知改由通知模組發送，建立當下為未發送，故不再以 IsSent 過濾
        return await _context.Notifications
            .Where(n => n.SourceReportID == reportId && !n.IsDeleted)
            .OrderByDescending(n => n.NotificationID)
            .ToListAsync();
    }


    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<(int pending, int approved, int rejected)> GetStatusCountsAsync()
    {
        var counts = await _context.Reports
            .Where(r => !r.IsDeleted)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int pending = counts.FirstOrDefault(c => c.Status == "Pending")?.Count ?? 0;
        int approved = counts.FirstOrDefault(c => c.Status == "Approved")?.Count ?? 0;
        int rejected = counts.FirstOrDefault(c => c.Status == "Rejected")?.Count ?? 0;

        return (pending, approved, rejected);
    }

    public async Task<int> GetPendingCountSinceAsync(DateTime since)
    {
        return await _context.Reports
            .CountAsync(r => !r.IsDeleted && r.Status == "Pending" && r.CreatedAt >= since);
    }

    public async Task<List<ReportStatusDatePoint>> GetStatusDatesSinceAsync(DateTime since)
    {
        return await _context.Reports
            .Where(r => !r.IsDeleted && r.CreatedAt >= since)
            .Select(r => new ReportStatusDatePoint { Status = r.Status, CreatedAt = r.CreatedAt })
            .ToListAsync();
    }
}
