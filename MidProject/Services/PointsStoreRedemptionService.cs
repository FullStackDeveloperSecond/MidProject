using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models.ViewModels.PointsStore;
using MidProject.Services.IServices;

namespace MidProject.Services;

public class PointsStoreRedemptionService : IPointsStoreRedemptionService
{
    private const int PageSize = 20;

    private readonly AppDbContext _dbContext;
    private readonly ITaipeiClock _clock;

    public PointsStoreRedemptionService(AppDbContext dbContext, ITaipeiClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<RedemptionsIndexViewModel> GetIndexAsync(
        string? keyword,
        int? frameFilter,
        DateOnly? startDate,
        DateOnly? endDate,
        int page = 1)
    {
        if (page < 1)
        {
            page = 1;
        }

        string? dateRangeError = null;
        if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
        {
            dateRangeError = "起始日期不能晚於結束日期，請重新選擇。";
        }

        var rows = new List<RedemptionRowViewModel>();
        var totalItems = 0;

        if (dateRangeError == null)
        {
            var query = _dbContext.PointsTransactions
                .Include(t => t.Member!).ThenInclude(m => m.AvatarImage)
                .Include(t => t.RelatedFrame!).ThenInclude(f => f.Image)
                .Where(t => t.Type == "Redeem");

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(t =>
                    t.Member!.UserName.Contains(keyword) ||
                    (t.Member!.NickName != null && t.Member!.NickName.Contains(keyword)));
            }

            if (frameFilter.HasValue)
            {
                query = query.Where(t => t.RelatedFrameID == frameFilter.Value);
            }

            if (startDate.HasValue)
            {
                var start = startDate.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(t => t.CreatedAt >= start);
            }

            if (endDate.HasValue)
            {
                var end = endDate.Value.ToDateTime(TimeOnly.MaxValue);
                query = query.Where(t => t.CreatedAt <= end);
            }

            query = query.OrderByDescending(t => t.CreatedAt);

            totalItems = await query.CountAsync();
            var pageItems = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            rows = pageItems.Select(t => new RedemptionRowViewModel
            {
                RedeemedAt = t.CreatedAt,
                MemberID = t.MemberID,
                MemberName = t.Member?.NickName ?? t.Member?.UserName ?? string.Empty,
                MemberAvatarUrl = t.Member?.AvatarImage?.ImageURL,
                FrameID = t.RelatedFrameID ?? 0,
                FrameName = t.RelatedFrame?.Name ?? string.Empty,
                FrameImageUrl = t.RelatedFrame?.Image?.ImageURL,
                PointsSpent = -t.Amount,
                BalanceAfter = t.BalanceAfter
            }).ToList();
        }

        var now = _clock.GetNow();
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthlyQuery = _dbContext.PointsTransactions
            .Where(t => t.Type == "Redeem" && t.CreatedAt >= monthStart);

        var monthlyRedemptionCount = await monthlyQuery.CountAsync();
        var monthlyPointsSpent = -(await monthlyQuery.SumAsync(t => t.Amount));
        var distinctMemberCount = await monthlyQuery.Select(t => t.MemberID).Distinct().CountAsync();

        string? topFrameName = null;
        var topFrameCount = 0;
        var topGroup = await monthlyQuery
            .Where(t => t.RelatedFrameID.HasValue)
            .GroupBy(t => t.RelatedFrameID!.Value)
            .Select(g => new { FrameID = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .FirstOrDefaultAsync();
        if (topGroup != null)
        {
            var topFrame = await _dbContext.AvatarFrames.FirstOrDefaultAsync(f => f.FrameID == topGroup.FrameID);
            topFrameName = topFrame?.Name;
            topFrameCount = topGroup.Count;
        }

        var availableFrames = await _dbContext.AvatarFrames
            .Where(f => !f.IsDeleted)
            .OrderBy(f => f.Name)
            .ToListAsync();

        return new RedemptionsIndexViewModel
        {
            MonthlyRedemptionCount = monthlyRedemptionCount,
            MonthlyPointsSpent = monthlyPointsSpent,
            TopFrameName = topFrameName,
            TopFrameCount = topFrameCount,
            DistinctMemberCount = distinctMemberCount,
            Redemptions = rows,
            AvailableFrames = availableFrames,
            Keyword = keyword,
            FrameFilter = frameFilter,
            StartDate = startDate,
            EndDate = endDate,
            DateRangeError = dateRangeError,
            CurrentPage = page,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize)
        };
    }
}
