using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models.ViewModels.PointsStore;
using MidProject.Services.IServices;

namespace MidProject.Services;

public class PointsStoreRedemptionService : IPointsStoreRedemptionService
{
    private const int PageSize = 20;

    private readonly AppDbContext _dbContext;

    public PointsStoreRedemptionService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RedemptionsIndexViewModel> GetIndexAsync(
        string? keyword,
        int? frameFilter,
        DateOnly? startDate,
        DateOnly? endDate,
        int page = 1)
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

        var totalItems = await query.CountAsync();
        var pageItems = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var rows = pageItems.Select(t => new RedemptionRowViewModel
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

        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var monthlyRedemptions = await _dbContext.PointsTransactions
            .Where(t => t.Type == "Redeem" && t.CreatedAt >= monthStart)
            .ToListAsync();

        string? topFrameName = null;
        var topFrameCount = 0;
        var topGroup = monthlyRedemptions
            .Where(t => t.RelatedFrameID.HasValue)
            .GroupBy(t => t.RelatedFrameID!.Value)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        if (topGroup != null)
        {
            var topFrame = await _dbContext.AvatarFrames.FirstOrDefaultAsync(f => f.FrameID == topGroup.Key);
            topFrameName = topFrame?.Name;
            topFrameCount = topGroup.Count();
        }

        var availableFrames = await _dbContext.AvatarFrames
            .Where(f => !f.IsDeleted)
            .OrderBy(f => f.Name)
            .ToListAsync();

        return new RedemptionsIndexViewModel
        {
            MonthlyRedemptionCount = monthlyRedemptions.Count,
            MonthlyPointsSpent = -monthlyRedemptions.Sum(t => t.Amount),
            TopFrameName = topFrameName,
            TopFrameCount = topFrameCount,
            DistinctMemberCount = monthlyRedemptions.Select(t => t.MemberID).Distinct().Count(),
            Redemptions = rows,
            AvailableFrames = availableFrames,
            Keyword = keyword,
            FrameFilter = frameFilter,
            StartDate = startDate,
            EndDate = endDate,
            CurrentPage = page,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize)
        };
    }
}
