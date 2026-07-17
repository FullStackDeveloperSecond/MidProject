using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models.DTOs;
using MidProject.Models;

namespace MidProject.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly AppDbContext _context;

    public ReportRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<Report>> GetReportsAsync(ReportQueryParams query)
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

        // 依指定欄位排序，預設依檢舉日期新到舊
        q = (query.SortBy, query.SortDirection?.ToLower()) switch
        {
            ("ReportID", "asc") => q.OrderBy(r => r.ReportID),
            ("ReportID", "desc") => q.OrderByDescending(r => r.ReportID),
            ("Status", "asc") => q.OrderBy(r => r.Status),
            ("Status", "desc") => q.OrderByDescending(r => r.Status),
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
        return await _context.Reports
            .Include(r => r.ReporterMember)
            .Include(r => r.Restaurant).ThenInclude(rest => rest!.Member)
            .Include(r => r.Review).ThenInclude(rev => rev!.Member)
            .Include(r => r.Image).ThenInclude(img => img!.UploadedByMember)
            .Include(r => r.HandledByMember)
            .FirstOrDefaultAsync(r => r.ReportID == reportId && !r.IsDeleted);
    }

    public async Task AddAsync(Report report)
    {
        await _context.Reports.AddAsync(report);
    }

    public async Task AddNotificationAsync(Notification notification)
    {
        await _context.Notifications.AddAsync(notification);
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
