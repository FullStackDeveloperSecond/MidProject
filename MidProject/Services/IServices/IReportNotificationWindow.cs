namespace MidProject.Services.IServices;

public interface IReportNotificationWindow
{
    Task<ReportNotificationResult> CreateOutcomeNotificationsAsync(
        ReportNotificationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ReportNotificationRequest(
    int ReportID,
    int ReporterMemberID,
    int? ReportedMemberID,
    string Outcome,
    int HandledByAdminID,
    DateTime HandledAt,
    string ReporterTitle,
    string ReporterContent,
    string? ReportedMemberTitle,
    string? ReportedMemberContent);

public enum ReportNotificationClassification
{
    Created,
    AlreadyExists,
    InvalidInput,
    InvalidOutcome,
    MemberNotFound,
    MemberDeleted,
    AdminInvalid,
    Failed
}

public sealed record ReportNotificationResult(
    ReportNotificationClassification Classification,
    IReadOnlyList<int> NotificationIDs,
    string? SafeErrorCode,
    string CorrelationID);
