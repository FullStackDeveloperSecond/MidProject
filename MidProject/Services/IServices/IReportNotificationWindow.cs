namespace MidProject.Services.IServices;

public interface IReportNotificationWindow
{
    // 通知檢舉者：檢舉審核結果（Approved/Rejected）
    Task<ReportNotificationResult> CreateOutcomeNotificationAsync(
        ReportNotificationRequest request,
        CancellationToken cancellationToken = default);

    // 通知被檢舉會員：內容經審核成立（只在 Approved 時使用）
    Task<ReportNotificationResult> CreateReportedMemberNotificationAsync(
        ReportedMemberNotificationRequest request,
        CancellationToken cancellationToken = default);
}

// Title / Content 為管理員在通知視窗編輯後的內容；留空則由 window 套用預設範本
public sealed record ReportNotificationRequest(
    int ReportID,
    int ReporterMemberID,
    string Outcome,
    int HandledByAdminID,
    DateTime HandledAt,
    string? Title = null,
    string? Content = null);

public sealed record ReportedMemberNotificationRequest(
    int ReportID,
    int ReportedMemberID,
    string Outcome,
    int HandledByAdminID,
    DateTime HandledAt,
    string? Title = null,
    string? Content = null);

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
