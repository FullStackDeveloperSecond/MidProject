using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels.Notifications;
using MidProject.Repositories.IRepositories;

namespace MidProject.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private const int PageSize = 10;
    private readonly AppDbContext _dbContext;

    public NotificationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<NotificationQueryResult> QueryAsync(
        NotificationIndexQuery query,
        DateTime? scheduledMinute,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Notification> notifications = _dbContext.Notifications.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToUpper();
            notifications = notifications.Where(x =>
                x.Title.ToUpper().Contains(keyword) || x.Content.ToUpper().Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.NotificationType))
        {
            notifications = notifications.Where(x => x.NotificationType == query.NotificationType);
        }

        if (query.IsSent.HasValue)
        {
            notifications = notifications.Where(x => x.IsSent == query.IsSent.Value);
        }

        if (scheduledMinute.HasValue)
        {
            var end = scheduledMinute.Value.AddMinutes(1);
            notifications = notifications.Where(x => x.ScheduledAt >= scheduledMinute.Value && x.ScheduledAt < end);
        }

        notifications = query.DeletionScope switch
        {
            NotificationDeletionScope.Deleted => notifications.Where(x => x.IsDeleted),
            NotificationDeletionScope.All => notifications,
            _ => notifications.Where(x => !x.IsDeleted)
        };

        notifications = (query.SortField, query.SortDirection) switch
        {
            (NotificationSortField.CreatedAt, NotificationSortDirection.Descending) =>
                notifications.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.NotificationID),
            (NotificationSortField.CreatedAt, _) =>
                notifications.OrderBy(x => x.CreatedAt).ThenBy(x => x.NotificationID),
            (NotificationSortField.ScheduledAt, NotificationSortDirection.Descending) =>
                notifications.OrderByDescending(x => x.ScheduledAt).ThenByDescending(x => x.NotificationID),
            _ => notifications.OrderBy(x => x.ScheduledAt).ThenBy(x => x.NotificationID)
        };

        var totalCount = await notifications.CountAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var items = await Project(notifications)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        return new NotificationQueryResult(items, totalCount);
    }

    public async Task<NotificationSummaryRecord> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var summary = await _dbContext.Notifications
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new NotificationSummaryRecord(
                group.Count(notification => !notification.IsDeleted),
                group.Count(notification => !notification.IsDeleted && !notification.IsSent),
                group.Count(notification => !notification.IsDeleted && notification.IsSent),
                group.Count(notification => notification.IsDeleted)))
            .SingleOrDefaultAsync(cancellationToken);

        return summary ?? new NotificationSummaryRecord(0, 0, 0, 0);
    }

    public Task<NotificationReadRecord?> GetReadAsync(int id, CancellationToken cancellationToken = default)
    {
        return Project(_dbContext.Notifications.AsNoTracking().Where(x => x.NotificationID == id))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Notification?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Notifications.SingleOrDefaultAsync(x => x.NotificationID == id, cancellationToken);
    }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications.AddAsync(notification, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);

    public void ClearTracking() => _dbContext.ChangeTracker.Clear();

    public Task<MemberLookup?> GetMemberAsync(int memberId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Members.AsNoTracking()
            .Where(x => x.MemberID == memberId)
            .Select(x => new MemberLookup(
                x.MemberID,
                !string.IsNullOrEmpty(x.NickName) ? x.NickName : x.UserName,
                x.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<UserLevelLookup?> GetLevelAsync(int levelId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserLevels.AsNoTracking()
            .Where(x => x.LevelID == levelId)
            .Select(x => new UserLevelLookup(x.LevelID, x.LevelName, x.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberLookup>> GetActiveMembersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Members.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.MemberID)
            .Select(x => new MemberLookup(
                x.MemberID,
                !string.IsNullOrEmpty(x.NickName) ? x.NickName : x.UserName,
                x.IsDeleted))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserLevelLookup>> GetActiveLevelsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserLevels.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.MinExp)
            .Select(x => new UserLevelLookup(x.LevelID, x.LevelName, x.IsDeleted))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsUsableAdminAsync(int memberId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Members.AsNoTracking().AnyAsync(x =>
            x.MemberID == memberId &&
            x.Role == "Admin" &&
            x.Status == "Normal" &&
            x.IsActive &&
            !x.IsLocked &&
            !x.IsDeleted,
            cancellationToken);
    }

    public Task<bool> ReportSourceExistsAsync(
        int reportId,
        string outcome,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Notifications.AsNoTracking().AnyAsync(x =>
            x.SourceReportID == reportId &&
            x.SourceReportOutcome == outcome,
            cancellationToken);
    }

    public Task<bool> ReportSourceExistsForMemberAsync(int reportId, string outcome, int memberId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Notifications.AsNoTracking().AnyAsync(x =>
            x.SourceReportID == reportId && x.SourceReportOutcome == outcome && x.MemberID == memberId,
            cancellationToken);
    }

    public Task<NotificationSendState?> GetSendStateAsync(int id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Notifications.AsNoTracking()
            .Where(x => x.NotificationID == id)
            .Select(x => new NotificationSendState(x.IsSent, x.IsDeleted, x.ScheduledAt, x.NotificationType, x.MemberID))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<int> TryMarkSentAsync(
        int id,
        DateTime nextMinute,
        DateTime sentAt,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Notifications
            .Where(x => x.NotificationID == id && !x.IsSent && !x.IsDeleted && x.ScheduledAt < nextMinute)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.IsSent, true)
                    .SetProperty(x => x.SentAt, sentAt),
                cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationSendCandidate>> GetDueCandidatesAsync(DateTime nextMinute, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications.AsNoTracking()
            .Where(x => !x.IsSent && !x.IsDeleted && x.ScheduledAt < nextMinute)
            .OrderBy(x => x.ScheduledAt)
            .ThenBy(x => x.NotificationID)
            .Select(x => new NotificationSendCandidate(x.NotificationID, x.NotificationType, x.MemberID))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Notifications.AsNoTracking()
            .CountAsync(x => !x.IsSent && !x.IsDeleted, cancellationToken);
    }

    private static IQueryable<NotificationReadRecord> Project(IQueryable<Notification> query)
    {
        return query.Select(x => new NotificationReadRecord
        {
            NotificationID = x.NotificationID,
            MemberID = x.MemberID,
            NotificationType = x.NotificationType,
            TargetRole = x.TargetRole,
            TargetStatus = x.TargetStatus,
            TargetLevelID = x.TargetLevelID,
            Title = x.Title,
            Content = x.Content,
            ScheduledAt = x.ScheduledAt,
            SentAt = x.SentAt,
            IsSent = x.IsSent,
            CreatedAt = x.CreatedAt,
            CreatedBy = x.CreatedBy,
            IsDeleted = x.IsDeleted,
            DeletedAt = x.DeletedAt,
            DeletedBy = x.DeletedBy,
            SourceReportID = x.SourceReportID,
            SourceReportOutcome = x.SourceReportOutcome,
            RowVersion = x.RowVersion,
            MemberExists = x.Member != null,
            MemberIsDeleted = x.Member != null && x.Member.IsDeleted,
            MemberName = x.Member == null ? null : (!string.IsNullOrEmpty(x.Member.NickName) ? x.Member.NickName : x.Member.UserName),
            LevelExists = x.TargetLevel != null,
            LevelIsDeleted = x.TargetLevel != null && x.TargetLevel.IsDeleted,
            LevelName = x.TargetLevel == null ? null : x.TargetLevel.LevelName
        });
    }
}
