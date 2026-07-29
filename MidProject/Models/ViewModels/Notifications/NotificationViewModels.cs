using System.ComponentModel.DataAnnotations;

namespace MidProject.Models.ViewModels.Notifications;

public enum NotificationDeletionScope
{
    Active,
    Deleted,
    All
}

public enum NotificationSortField
{
    ScheduledAt,
    CreatedAt
}

public enum NotificationSortDirection
{
    Ascending,
    Descending
}

public sealed class NotificationIndexQuery
{
    public string? Keyword { get; set; }
    public string? NotificationType { get; set; }
    public bool? IsSent { get; set; }
    public DateTime? ScheduledAtMinute { get; set; }
    public NotificationDeletionScope DeletionScope { get; set; } = NotificationDeletionScope.Active;
    public NotificationSortField SortField { get; set; } = NotificationSortField.ScheduledAt;
    public NotificationSortDirection SortDirection { get; set; } = NotificationSortDirection.Ascending;
    public int Page { get; set; } = 1;
}

public sealed class NotificationIndexViewModel
{
    public required NotificationIndexQuery Query { get; init; }
    public required NotificationSummaryViewModel Summary { get; init; }
    public required IReadOnlyList<NotificationIndexRowViewModel> Items { get; init; }
    public int TotalCount { get; init; }
    public int PageSize { get; init; } = 10;
    public string? QueryError { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed class NotificationSummaryViewModel
{
    public int ActiveCount { get; init; }
    public int UnsentCount { get; init; }
    public int SentCount { get; init; }
    public int DeletedCount { get; init; }
}

public sealed class NotificationIndexRowViewModel
{
    public int NotificationID { get; init; }
    public required string NotificationType { get; init; }
    public required string Title { get; init; }
    public required string AudienceSummary { get; init; }
    public DateTime ScheduledAt { get; init; }
    public bool IsSent { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public bool CanMutate => !IsSent && !IsDeleted;
}

public sealed class NotificationFormViewModel
{
    public int? NotificationID { get; set; }
    public string? RowVersion { get; set; }

    [Required(ErrorMessage = "通知類型為必填。")]
    public string NotificationType { get; set; } = "Personal";

    public int? MemberID { get; set; }
    public string? TargetRole { get; set; }
    public string? TargetStatus { get; set; }
    public int? TargetLevelID { get; set; }

    [Required(ErrorMessage = "標題為必填。")]
    [StringLength(100, ErrorMessage = "標題最多 100 個字元。")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "內容為必填。")]
    [StringLength(1000, ErrorMessage = "內容最多 1000 個字元。")]
    public string Content { get; set; } = string.Empty;

    [Required(ErrorMessage = "排程時間為必填。")]
    public DateTime? ScheduledAt { get; set; }

    public IReadOnlyList<NotificationOptionViewModel> MemberOptions { get; set; } = [];
    public IReadOnlyList<NotificationOptionViewModel> LevelOptions { get; set; } = [];
    public IReadOnlyList<NotificationOptionViewModel> RoleOptions { get; set; } = [];
    public IReadOnlyList<NotificationOptionViewModel> StatusOptions { get; set; } = [];
}

public sealed record NotificationOptionViewModel(string Value, string Label);

public sealed class NotificationDetailsViewModel
{
    public int NotificationID { get; init; }
    public int? MemberID { get; init; }
    public required string NotificationType { get; init; }
    public string? TargetRole { get; init; }
    public string? TargetStatus { get; init; }
    public int? TargetLevelID { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required string AudienceSummary { get; init; }
    public DateTime ScheduledAt { get; init; }
    public DateTime? SentAt { get; init; }
    public bool IsSent { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CreatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public int? DeletedBy { get; init; }
    public string? SourceReportOutcome { get; init; }
    public int? SourceReportID { get; init; }
    public required string RowVersion { get; init; }
    public bool CanMutate => !IsSent && !IsDeleted;
}

public sealed class NotificationDeleteViewModel
{
    public int NotificationID { get; init; }
    public required string Title { get; init; }
    public required string NotificationType { get; init; }
    public required string AudienceSummary { get; init; }
    public DateTime ScheduledAt { get; init; }
    public required string RowVersion { get; init; }
}
