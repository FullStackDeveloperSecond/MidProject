using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class ReportNotificationWindow : IReportNotificationWindow
{
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

    public async Task<ReportNotificationResult> CreateOutcomeNotificationsAsync(
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

        if (!HasValidMessage(request.ReporterTitle, request.ReporterContent))
        {
            return Result(ReportNotificationClassification.InvalidInput, correlationId, "INVALID_REPORTER_MESSAGE");
        }

        if (request.Outcome == "Approved" &&
            (!request.ReportedMemberID.HasValue ||
             !HasValidMessage(request.ReportedMemberTitle, request.ReportedMemberContent)))
        {
            return Result(ReportNotificationClassification.InvalidInput, correlationId, "INVALID_REPORTED_MEMBER_MESSAGE");
        }

        if (!await _repository.IsUsableAdminAsync(request.HandledByAdminID, cancellationToken))
        {
            return Result(ReportNotificationClassification.AdminInvalid, correlationId, "ADMIN_INVALID");
        }

        var recipients = new List<ReportNotificationRecipient>
        {
            new(request.ReporterMemberID, request.ReporterTitle.Trim(), request.ReporterContent.Trim())
        };

        if (request.Outcome == "Approved" &&
            request.ReportedMemberID is int reportedMemberId &&
            reportedMemberId != request.ReporterMemberID)
        {
            recipients.Add(new(
                reportedMemberId,
                request.ReportedMemberTitle!.Trim(),
                request.ReportedMemberContent!.Trim()));
        }

        foreach (var recipient in recipients)
        {
            var member = await _repository.GetMemberAsync(recipient.MemberID, cancellationToken);
            if (member is null)
            {
                return Result(ReportNotificationClassification.MemberNotFound, correlationId, "MEMBER_NOT_FOUND");
            }

            if (member.IsDeleted)
            {
                return Result(ReportNotificationClassification.MemberDeleted, correlationId, "MEMBER_DELETED");
            }
        }

        var existingRecipientIds = new HashSet<int>();
        foreach (var recipient in recipients)
        {
            if (await _repository.ReportSourceExistsAsync(
                    request.ReportID,
                    request.Outcome,
                    recipient.MemberID,
                    cancellationToken))
            {
                existingRecipientIds.Add(recipient.MemberID);
            }
        }

        if (existingRecipientIds.Count == recipients.Count)
        {
            return Result(ReportNotificationClassification.AlreadyExists, correlationId);
        }

        var createdNotifications = recipients
            .Where(recipient => !existingRecipientIds.Contains(recipient.MemberID))
            .Select(recipient => new Notification
            {
                NotificationType = "Personal",
                MemberID = recipient.MemberID,
                Title = recipient.Title,
                Content = recipient.Content,
                ScheduledAt = _clock.NormalizeMinute(request.HandledAt),
                IsSent = false,
                SentAt = null,
                CreatedAt = _clock.GetNow(),
                CreatedBy = request.HandledByAdminID,
                IsDeleted = false,
                SourceReportID = request.ReportID,
                SourceReportOutcome = request.Outcome
            })
            .ToList();

        try
        {
            foreach (var notification in createdNotifications)
            {
                await _repository.AddAsync(notification, cancellationToken);
            }

            await _repository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Report notifications created at {TaipeiTimestamp}; Operation=ReportCreate; NotificationIDs={NotificationIDs}; AdminID={AdminID}; ResultClassification=Created; CorrelationID={CorrelationID}; MemberIDs={MemberIDs}",
                _clock.GetNow(),
                createdNotifications.Select(x => x.NotificationID).ToArray(),
                request.HandledByAdminID,
                correlationId,
                createdNotifications.Select(x => x.MemberID).ToArray());
            return new(
                ReportNotificationClassification.Created,
                createdNotifications.Select(x => x.NotificationID).ToArray(),
                null,
                correlationId);
        }
        catch (DbUpdateException ex)
        {
            _repository.ClearTracking();
            var allRecipientsExist = true;
            foreach (var recipient in recipients)
            {
                if (!await _repository.ReportSourceExistsAsync(
                        request.ReportID,
                        request.Outcome,
                        recipient.MemberID,
                        cancellationToken))
                {
                    allRecipientsExist = false;
                    break;
                }
            }

            if (allRecipientsExist)
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

    private static bool HasValidMessage(string? title, string? content) =>
        !string.IsNullOrWhiteSpace(title) &&
        title.Trim().Length <= 100 &&
        !string.IsNullOrWhiteSpace(content) &&
        content.Trim().Length <= 1000;

    private static ReportNotificationResult Result(
        ReportNotificationClassification classification,
        string correlationId,
        string? safeErrorCode = null) => new(classification, Array.Empty<int>(), safeErrorCode, correlationId);

    private void LogFailure(ReportNotificationRequest request, string correlationId, Exception exception)
    {
        _logger.LogError(
            exception,
            "Report notification failure at {TaipeiTimestamp}; Operation=ReportCreate; NotificationID={NotificationID}; AdminID={AdminID}; ResultClassification=Failed; CorrelationID={CorrelationID}; MemberID={MemberID}; ExceptionType={ExceptionType}",
            _clock.GetNow(), null, request.HandledByAdminID, correlationId, request.ReporterMemberID, exception.GetType().Name);
    }

    private sealed record ReportNotificationRecipient(int MemberID, string Title, string Content);
}
