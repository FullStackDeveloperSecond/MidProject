using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class ReportNotificationWindow : IReportNotificationWindow
{
    private const string FixedTitle = "【檢舉結果通知】您提交的檢舉已完成審核";
    private readonly INotificationRepository _repository;
    private readonly ITaipeiClock _clock;
    private readonly ILogger<ReportNotificationWindow> _logger;

    public ReportNotificationWindow(
        INotificationRepository repository,
        ITaipeiClock clock,
        ILogger<ReportNotificationWindow> logger)
    {
        _repository = repository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ReportNotificationResult> CreateOutcomeNotificationAsync(
        ReportNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        if (request.ReportID <= 0 || request.ReporterMemberID <= 0 || request.HandledByAdminID <= 0 || request.HandledAt == default)
        {
            return Result(ReportNotificationClassification.InvalidInput, correlationId, "INVALID_INPUT");
        }

        if (request.Outcome is not ("Approved" or "Rejected"))
        {
            return Result(ReportNotificationClassification.InvalidOutcome, correlationId, "INVALID_OUTCOME");
        }

        var member = await _repository.GetMemberAsync(request.ReporterMemberID, cancellationToken);
        if (member is null)
        {
            return Result(ReportNotificationClassification.MemberNotFound, correlationId, "MEMBER_NOT_FOUND");
        }

        if (member.IsDeleted)
        {
            return Result(ReportNotificationClassification.MemberDeleted, correlationId, "MEMBER_DELETED");
        }

        if (!await _repository.IsUsableAdminAsync(request.HandledByAdminID, cancellationToken))
        {
            return Result(ReportNotificationClassification.AdminInvalid, correlationId, "ADMIN_INVALID");
        }

        if (await _repository.ReportSourceExistsAsync(request.ReportID, request.Outcome, cancellationToken))
        {
            return Result(ReportNotificationClassification.AlreadyExists, correlationId);
        }

        var notification = new Notification
        {
            NotificationType = "Personal",
            MemberID = request.ReporterMemberID,
            Title = FixedTitle,
            Content = request.Outcome == "Approved"
                ? "您提交的檢舉已完成審核，審核結果為通過（Approved）。"
                : "您提交的檢舉已完成審核，審核結果為駁回（Rejected）。",
            ScheduledAt = _clock.NormalizeMinute(request.HandledAt),
            IsSent = false,
            SentAt = null,
            CreatedAt = _clock.GetNow(),
            CreatedBy = request.HandledByAdminID,
            IsDeleted = false,
            SourceReportID = request.ReportID,
            SourceReportOutcome = request.Outcome
        };

        try
        {
            await _repository.AddAsync(notification, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Report notification created at {TaipeiTimestamp}; Operation=ReportCreate; NotificationID={NotificationID}; AdminID={AdminID}; ResultClassification=Created; CorrelationID={CorrelationID}; MemberID={MemberID}",
                _clock.GetNow(), notification.NotificationID, request.HandledByAdminID, correlationId, request.ReporterMemberID);
            return new(ReportNotificationClassification.Created, notification.NotificationID, null, correlationId);
        }
        catch (DbUpdateException ex)
        {
            _repository.ClearTracking();
            if (await _repository.ReportSourceExistsAsync(request.ReportID, request.Outcome, cancellationToken))
            {
                return Result(ReportNotificationClassification.AlreadyExists, correlationId);
            }

            LogFailure(request, correlationId, ex);
            return Result(ReportNotificationClassification.Failed, correlationId, "PERSISTENCE_ERROR");
        }
        catch (Exception ex)
        {
            _repository.ClearTracking();
            LogFailure(request, correlationId, ex);
            return Result(ReportNotificationClassification.Failed, correlationId, "UNEXPECTED_ERROR");
        }
    }

    private static ReportNotificationResult Result(
        ReportNotificationClassification classification,
        string correlationId,
        string? safeErrorCode = null) => new(classification, null, safeErrorCode, correlationId);

    private void LogFailure(ReportNotificationRequest request, string correlationId, Exception exception)
    {
        _logger.LogError(
            exception,
            "Report notification failure at {TaipeiTimestamp}; Operation=ReportCreate; NotificationID={NotificationID}; AdminID={AdminID}; ResultClassification=Failed; CorrelationID={CorrelationID}; MemberID={MemberID}; ExceptionType={ExceptionType}",
            _clock.GetNow(), null, request.HandledByAdminID, correlationId, request.ReporterMemberID, exception.GetType().Name);
    }
}
