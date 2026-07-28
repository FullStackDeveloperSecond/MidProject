using MidProject.Models;
using MidProject.Models.ViewModels.Notifications;

namespace MidProject.Repositories.IRepositories;

public interface INotificationRepository
{
    Task<NotificationQueryResult> QueryAsync(NotificationIndexQuery query, DateTime? scheduledMinute, CancellationToken cancellationToken = default);
    Task<NotificationReadRecord?> GetReadAsync(int id, CancellationToken cancellationToken = default);
    Task<Notification?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    void ClearTracking();
    Task<MemberLookup?> GetMemberAsync(int memberId, CancellationToken cancellationToken = default);
    Task<UserLevelLookup?> GetLevelAsync(int levelId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberLookup>> GetActiveMembersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserLevelLookup>> GetActiveLevelsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsUsableAdminAsync(int memberId, CancellationToken cancellationToken = default);
    Task<bool> ReportSourceExistsAsync(int reportId, string outcome, int memberId, CancellationToken cancellationToken = default);
    Task<NotificationSendState?> GetSendStateAsync(int id, CancellationToken cancellationToken = default);
    Task<int> TryMarkSentAsync(int id, DateTime nextMinute, DateTime sentAt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationSendCandidate>> GetDueCandidatesAsync(DateTime nextMinute, CancellationToken cancellationToken = default);
    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);
}

public sealed record NotificationQueryResult(IReadOnlyList<NotificationReadRecord> Items, int TotalCount);

public sealed class NotificationReadRecord
{
    public int NotificationID { get; init; }
    public int? MemberID { get; init; }
    public required string NotificationType { get; init; }
    public string? TargetRole { get; init; }
    public string? TargetStatus { get; init; }
    public int? TargetLevelID { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public DateTime ScheduledAt { get; init; }
    public DateTime? SentAt { get; init; }
    public bool IsSent { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public int? DeletedBy { get; init; }
    public int? SourceReportID { get; init; }
    public string? SourceReportOutcome { get; init; }
    public required byte[] RowVersion { get; init; }
    public bool MemberExists { get; init; }
    public bool MemberIsDeleted { get; init; }
    public string? MemberName { get; init; }
    public bool LevelExists { get; init; }
    public bool LevelIsDeleted { get; init; }
    public string? LevelName { get; init; }
}

public sealed record MemberLookup(int MemberID, string DisplayName, bool IsDeleted);
public sealed record UserLevelLookup(int LevelID, string LevelName, bool IsDeleted);
public sealed record NotificationSendState(
    bool IsSent,
    bool IsDeleted,
    DateTime ScheduledAt,
    string NotificationType,
    int? MemberID);
public sealed record NotificationSendCandidate(int NotificationID, string NotificationType, int? MemberID);
