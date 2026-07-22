namespace MidProject.Services.IServices;

public interface IReportNotificationWindow
{
    Task<ReportNotificationResult> CreateOutcomeNotificationAsync(
        ReportNotificationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ReportNotificationRequest(
    int ReportID,
    int ReporterMemberID,
    string Outcome,
    int HandledByAdminID,
    DateTime HandledAt);

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
    int? NotificationID,
    string? SafeErrorCode,
    string CorrelationID);
