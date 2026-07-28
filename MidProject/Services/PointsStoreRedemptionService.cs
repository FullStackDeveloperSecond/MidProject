using System.Data;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels.PointsStore;
using MidProject.Services.IServices;

namespace MidProject.Services;

public class PointsStoreRedemptionService : IPointsStoreRedemptionService
{
    private const int PageSize = 20;

    private readonly AppDbContext _dbContext;
    private readonly ITaipeiClock _clock;
    private readonly ILogger<PointsStoreRedemptionService> _logger;

    public PointsStoreRedemptionService(
        AppDbContext dbContext,
        ITaipeiClock clock,
        ILogger<PointsStoreRedemptionService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _logger = logger;
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

    public async Task<PointsStoreRedeemResult> RedeemAsync(
        int memberId,
        int frameId,
        CancellationToken cancellationToken = default)
    {
        if (memberId <= 0)
        {
            return new(PointsStoreRedeemClassification.MemberNotFound);
        }

        if (frameId <= 0)
        {
            return new(PointsStoreRedeemClassification.FrameNotFound);
        }

        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var member = await _dbContext.Members
                .FirstOrDefaultAsync(
                    item => item.MemberID == memberId && !item.IsDeleted,
                    cancellationToken);
            if (member == null)
            {
                return new(PointsStoreRedeemClassification.MemberNotFound);
            }

            var frame = await _dbContext.AvatarFrames
                .FirstOrDefaultAsync(item => item.FrameID == frameId, cancellationToken);
            if (frame == null)
            {
                return new(PointsStoreRedeemClassification.FrameNotFound);
            }

            if (frame.IsDeleted || !frame.IsActive)
            {
                return new(PointsStoreRedeemClassification.FrameUnavailable);
            }

            if (frame.PointsPrice <= 0)
            {
                return new(PointsStoreRedeemClassification.InvalidFramePrice);
            }

            if (await _dbContext.MemberAvatarFrames.AnyAsync(
                    item => item.MemberID == memberId && item.FrameID == frameId,
                    cancellationToken))
            {
                return new(PointsStoreRedeemClassification.AlreadyOwned, member.Points);
            }

            if (member.Points < frame.PointsPrice)
            {
                return new(PointsStoreRedeemClassification.InsufficientPoints, member.Points);
            }

            var now = _clock.GetNow();
            member.Points -= frame.PointsPrice;
            member.UpdatedAt = now;

            var ownership = new MemberAvatarFrame
            {
                MemberID = member.MemberID,
                FrameID = frame.FrameID,
                RedeemedAt = now
            };
            var pointsTransaction = new PointsTransaction
            {
                MemberID = member.MemberID,
                Amount = -frame.PointsPrice,
                BalanceAfter = member.Points,
                Type = "Redeem",
                RelatedFrameID = frame.FrameID,
                Note = $"兌換外框：{frame.Name}",
                CreatedAt = now,
                CreatedBy = null
            };

            _dbContext.MemberAvatarFrames.Add(ownership);
            _dbContext.PointsTransactions.Add(pointsTransaction);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new(
                PointsStoreRedeemClassification.Redeemed,
                member.Points,
                pointsTransaction.TransactionID);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateException exception)
        {
            _dbContext.ChangeTracker.Clear();
            if (await _dbContext.MemberAvatarFrames
                .AsNoTracking()
                .AnyAsync(
                    item => item.MemberID == memberId && item.FrameID == frameId,
                    cancellationToken))
            {
                return new(PointsStoreRedeemClassification.AlreadyOwned);
            }

            LogRedeemFailure(memberId, frameId, exception);
            return new(
                PointsStoreRedeemClassification.Failed,
                SafeErrorCode: "REDEEM_PERSISTENCE_FAILED");
        }
        catch (Exception exception)
        {
            _dbContext.ChangeTracker.Clear();
            LogRedeemFailure(memberId, frameId, exception);
            return new(
                PointsStoreRedeemClassification.Failed,
                SafeErrorCode: "REDEEM_UNEXPECTED_FAILED");
        }
    }

    private void LogRedeemFailure(int memberId, int frameId, Exception exception)
    {
        _logger.LogError(
            exception,
            "Points store redemption failed at {TaipeiTimestamp}; Operation=RedeemAvatarFrame; MemberID={MemberID}; FrameID={FrameID}; ExceptionType={ExceptionType}",
            _clock.GetNow(),
            memberId,
            frameId,
            exception.GetType().Name);
    }
}
