using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Models.ViewModels.Notifications;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class NotificationService : INotificationService
{
    private const int PageSize = 10;
    private readonly INotificationRepository _repository;
    private readonly ITaipeiClock _clock;
    private readonly IMemberAudienceCatalog _catalog;
    private readonly NotificationPresenter _presenter;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository repository,
        ITaipeiClock clock,
        IMemberAudienceCatalog catalog,
        NotificationPresenter presenter,
        ILogger<NotificationService> logger)
    {
        _repository = repository;
        _clock = clock;
        _catalog = catalog;
        _presenter = presenter;
        _logger = logger;
    }

    public async Task<NotificationIndexViewModel> GetIndexAsync(
        NotificationIndexQuery query,
        CancellationToken cancellationToken = default)
    {
        query.Page = Math.Max(1, query.Page);
        query.Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();

        string? queryError = null;
        if (!string.IsNullOrEmpty(query.NotificationType) && query.NotificationType is not ("Personal" or "Condition"))
        {
            queryError = "通知類型篩選值無效。";
        }

        DateTime? scheduledMinute = query.ScheduledAtMinute.HasValue
            ? _clock.NormalizeMinute(query.ScheduledAtMinute.Value)
            : null;

        var result = await _repository.QueryAsync(query, scheduledMinute, cancellationToken);
        return new NotificationIndexViewModel
        {
            Query = query,
            TotalCount = result.TotalCount,
            PageSize = PageSize,
            QueryError = queryError,
            Items = result.Items.Select(ToIndexRow).ToList()
        };
    }

    public async Task<NotificationDetailsViewModel?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        var source = await _repository.GetReadAsync(id, cancellationToken);
        return source is null ? null : ToDetails(source);
    }

    public async Task<NotificationFormViewModel> GetCreateFormAsync(
        NotificationFormViewModel? submitted = null,
        CancellationToken cancellationToken = default)
    {
        var form = submitted ?? new NotificationFormViewModel
        {
            NotificationType = "Personal",
            ScheduledAt = _clock.GetCurrentMinute().AddMinutes(1)
        };
        await HydrateOptionsAsync(form, cancellationToken);
        return form;
    }

    public async Task<NotificationFormViewModel?> GetEditFormAsync(
        int id,
        NotificationFormViewModel? submitted = null,
        CancellationToken cancellationToken = default)
    {
        if (submitted is not null)
        {
            submitted.NotificationID = id;
            await HydrateOptionsAsync(submitted, cancellationToken);
            return submitted;
        }

        var source = await _repository.GetReadAsync(id, cancellationToken);
        if (source is null || source.IsSent || source.IsDeleted)
        {
            return null;
        }

        var form = new NotificationFormViewModel
        {
            NotificationID = source.NotificationID,
            RowVersion = Convert.ToBase64String(source.RowVersion),
            NotificationType = source.NotificationType,
            MemberID = source.MemberID,
            TargetRole = source.TargetRole,
            TargetStatus = source.TargetStatus,
            TargetLevelID = source.TargetLevelID,
            Title = source.Title,
            Content = source.Content,
            ScheduledAt = source.ScheduledAt
        };
        await HydrateOptionsAsync(form, cancellationToken);
        return form;
    }

    public async Task<NotificationDeleteViewModel?> GetDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var source = await _repository.GetReadAsync(id, cancellationToken);
        if (source is null || source.IsSent || source.IsDeleted)
        {
            return null;
        }

        return new NotificationDeleteViewModel
        {
            NotificationID = source.NotificationID,
            Title = source.Title,
            NotificationType = source.NotificationType,
            AudienceSummary = _presenter.GetAudienceSummary(source),
            ScheduledAt = source.ScheduledAt,
            RowVersion = Convert.ToBase64String(source.RowVersion)
        };
    }

    public async Task<NotificationCommandResult> CreateAsync(
        NotificationFormViewModel form,
        int adminId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.GetNow();
        var errors = await ValidateFormAsync(form, now, cancellationToken);
        if (!await _repository.IsUsableAdminAsync(adminId, cancellationToken))
        {
            AddError(errors, string.Empty, "無法確認有效的管理員身分。");
        }

        if (errors.Count != 0)
        {
            LogOperation("Create", null, adminId, "ValidationFailed", correlationId, form.NotificationType, form.MemberID);
            return new(NotificationCommandClassification.ValidationFailed, ValidationErrors: Freeze(errors));
        }

        var scheduledAt = _clock.NormalizeMinute(form.ScheduledAt!.Value);
        var currentMinute = _clock.NormalizeMinute(now);
        var immediate = scheduledAt == currentMinute;
        var notification = new Notification
        {
            NotificationType = form.NotificationType.Trim(),
            MemberID = form.MemberID,
            TargetRole = NullIfEmpty(form.TargetRole),
            TargetStatus = NullIfEmpty(form.TargetStatus),
            TargetLevelID = form.TargetLevelID,
            Title = form.Title.Trim(),
            Content = form.Content.Trim(),
            ScheduledAt = scheduledAt,
            IsSent = immediate,
            SentAt = immediate ? now : null,
            CreatedAt = now,
            CreatedBy = adminId,
            IsDeleted = false
        };

        try
        {
            await _repository.AddAsync(notification, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            LogOperation("Create", notification.NotificationID, adminId, "Success", correlationId, notification.NotificationType, notification.MemberID);
            return new(NotificationCommandClassification.Success, notification.NotificationID);
        }
        catch (Exception ex)
        {
            _repository.ClearTracking();
            LogFailure("Create", null, adminId, correlationId, form.NotificationType, form.MemberID, ex);
            return new(NotificationCommandClassification.Failed);
        }
    }

    public async Task<NotificationCommandResult> EditAsync(
        int id,
        NotificationFormViewModel form,
        int adminId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.GetNow();
        var errors = await ValidateFormAsync(form, now, cancellationToken);
        var postedVersion = ParseRowVersion(form.RowVersion);
        if (postedVersion is null)
        {
            AddError(errors, nameof(form.RowVersion), "資料版本無效，請重新整理後再試。");
        }

        if (errors.Count != 0)
        {
            LogOperation("Edit", id, adminId, "ValidationFailed", correlationId, form.NotificationType, form.MemberID);
            return new(NotificationCommandClassification.ValidationFailed, id, Freeze(errors));
        }

        var notification = await _repository.GetForUpdateAsync(id, cancellationToken);
        if (notification is null)
        {
            return new(NotificationCommandClassification.NotFound, id);
        }

        if (notification.IsDeleted)
        {
            return new(NotificationCommandClassification.Deleted, id);
        }

        if (notification.IsSent)
        {
            return new(NotificationCommandClassification.ReadOnly, id);
        }

        if (!notification.RowVersion.SequenceEqual(postedVersion!))
        {
            return new(NotificationCommandClassification.ConcurrentlyHandled, id);
        }

        var scheduledAt = _clock.NormalizeMinute(form.ScheduledAt!.Value);
        var immediate = scheduledAt == _clock.NormalizeMinute(now);
        notification.NotificationType = form.NotificationType.Trim();
        notification.MemberID = form.MemberID;
        notification.TargetRole = NullIfEmpty(form.TargetRole);
        notification.TargetStatus = NullIfEmpty(form.TargetStatus);
        notification.TargetLevelID = form.TargetLevelID;
        notification.Title = form.Title.Trim();
        notification.Content = form.Content.Trim();
        notification.ScheduledAt = scheduledAt;
        notification.IsSent = immediate;
        notification.SentAt = immediate ? now : null;

        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
            LogOperation("Edit", id, adminId, "Success", correlationId, notification.NotificationType, notification.MemberID);
            return new(NotificationCommandClassification.Success, id);
        }
        catch (DbUpdateConcurrencyException)
        {
            _repository.ClearTracking();
            LogOperation("Edit", id, adminId, "ConcurrentlyHandled", correlationId, form.NotificationType, form.MemberID);
            return new(NotificationCommandClassification.ConcurrentlyHandled, id);
        }
        catch (Exception ex)
        {
            _repository.ClearTracking();
            LogFailure("Edit", id, adminId, correlationId, form.NotificationType, form.MemberID, ex);
            return new(NotificationCommandClassification.Failed, id);
        }
    }

    public async Task<NotificationCommandResult> DeleteAsync(
        int id,
        string rowVersion,
        int adminId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!await _repository.IsUsableAdminAsync(adminId, cancellationToken))
        {
            return new(NotificationCommandClassification.ValidationFailed, id,
                new Dictionary<string, string[]> { [string.Empty] = ["無法確認有效的管理員身分。"] });
        }

        var postedVersion = ParseRowVersion(rowVersion);
        if (postedVersion is null)
        {
            return new(NotificationCommandClassification.ConcurrentlyHandled, id);
        }

        var notification = await _repository.GetForUpdateAsync(id, cancellationToken);
        if (notification is null)
        {
            return new(NotificationCommandClassification.NotFound, id);
        }

        if (notification.IsDeleted)
        {
            return new(NotificationCommandClassification.Deleted, id);
        }

        if (notification.IsSent)
        {
            return new(NotificationCommandClassification.ReadOnly, id);
        }

        if (!notification.RowVersion.SequenceEqual(postedVersion))
        {
            return new(NotificationCommandClassification.ConcurrentlyHandled, id);
        }

        notification.IsDeleted = true;
        notification.DeletedAt = _clock.GetNow();
        notification.DeletedBy = adminId;

        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
            LogOperation("Delete", id, adminId, "Success", correlationId, notification.NotificationType, notification.MemberID);
            return new(NotificationCommandClassification.Success, id);
        }
        catch (DbUpdateConcurrencyException)
        {
            _repository.ClearTracking();
            LogOperation("Delete", id, adminId, "ConcurrentlyHandled", correlationId, notification.NotificationType, notification.MemberID);
            return new(NotificationCommandClassification.ConcurrentlyHandled, id);
        }
        catch (Exception ex)
        {
            _repository.ClearTracking();
            LogFailure("Delete", id, adminId, correlationId, notification.NotificationType, notification.MemberID, ex);
            return new(NotificationCommandClassification.Failed, id);
        }
    }

    public async Task<SingleSendResult> SendAsync(
        int id,
        int adminId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var state = await _repository.GetSendStateAsync(id, cancellationToken);
        if (state is null)
        {
            LogOperation("Send", id, adminId, "NotFound", correlationId, null, null);
            return new(SingleSendClassification.NotFound, id);
        }

        if (state.IsDeleted)
        {
            LogOperation("Send", id, adminId, "Deleted", correlationId, state.NotificationType, state.MemberID);
            return new(SingleSendClassification.Deleted, id);
        }

        if (state.IsSent)
        {
            LogOperation("Send", id, adminId, "AlreadySent", correlationId, state.NotificationType, state.MemberID);
            return new(SingleSendClassification.AlreadySent, id);
        }

        var now = _clock.GetNow();
        var nextMinute = _clock.NormalizeMinute(now).AddMinutes(1);
        if (state.ScheduledAt >= nextMinute)
        {
            LogOperation("Send", id, adminId, "NotDue", correlationId, state.NotificationType, state.MemberID);
            return new(SingleSendClassification.NotDue, id);
        }

        try
        {
            var affected = await _repository.TryMarkSentAsync(id, nextMinute, now, cancellationToken);
            var classification = affected == 1
                ? SingleSendClassification.Success
                : SingleSendClassification.ConcurrentlyHandled;
            LogOperation("Send", id, adminId, classification.ToString(), correlationId, state.NotificationType, state.MemberID);
            return new(classification, id);
        }
        catch (Exception ex)
        {
            LogFailure("Send", id, adminId, correlationId, state.NotificationType, state.MemberID, ex);
            return new(SingleSendClassification.Failed, id);
        }
    }

    public async Task<BatchSendResult> SendDueAsync(
        int adminId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.GetNow();
        var nextMinute = _clock.NormalizeMinute(now).AddMinutes(1);
        var candidates = await _repository.GetDueCandidatesAsync(nextMinute, cancellationToken);
        var items = new List<BatchSendItemResult>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var id = candidate.NotificationID;
            try
            {
                var affected = await _repository.TryMarkSentAsync(id, nextMinute, now, cancellationToken);
                if (affected == 1)
                {
                    items.Add(new(id, BatchItemClassification.Success));
                    LogOperation("BatchSendItem", id, adminId, "Success", correlationId, candidate.NotificationType, candidate.MemberID);
                    continue;
                }

                var state = await _repository.GetSendStateAsync(id, cancellationToken);
                var classification = state switch
                {
                    null => BatchItemClassification.Failed,
                    { IsDeleted: true } => BatchItemClassification.Deleted,
                    { IsSent: true } => BatchItemClassification.AlreadySent,
                    { ScheduledAt: var scheduledAt } when scheduledAt >= nextMinute => BatchItemClassification.NotDue,
                    _ => BatchItemClassification.Failed
                };
                items.Add(new(id, classification, classification == BatchItemClassification.Failed ? "STATE_CHANGED" : null));
                LogOperation("BatchSendItem", id, adminId, classification.ToString(), correlationId, candidate.NotificationType, candidate.MemberID);
            }
            catch (Exception ex)
            {
                items.Add(new(id, BatchItemClassification.Failed, "PERSISTENCE_ERROR"));
                LogFailure("BatchSendItem", id, adminId, correlationId, candidate.NotificationType, candidate.MemberID, ex);
            }
        }

        var result = new BatchSendResult { Items = items, CorrelationID = correlationId };
        _logger.LogInformation(
            "Notification batch completed at {TaipeiTimestamp}; Operation=BatchSend; AdminID={AdminID}; ResultClassification=Completed; SuccessCount={SuccessCount}; AlreadySentCount={AlreadySentCount}; NotDueCount={NotDueCount}; DeletedCount={DeletedCount}; FailedCount={FailedCount}; BatchCorrelationID={BatchCorrelationID}",
            now,
            adminId,
            result.SuccessCount,
            result.AlreadySentCount,
            result.NotDueCount,
            result.DeletedCount,
            result.FailedCount,
            correlationId);
        return result;
    }

    private async Task<Dictionary<string, List<string>>> ValidateFormAsync(
        NotificationFormViewModel form,
        DateTime requestTime,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var type = form.NotificationType?.Trim();
        if (string.IsNullOrWhiteSpace(type))
        {
            AddError(errors, nameof(form.NotificationType), "通知類型為必填。");
        }
        else if (type is not ("Personal" or "Condition"))
        {
            AddError(errors, nameof(form.NotificationType), "通知類型只允許 Personal 或 Condition。");
        }

        if (string.IsNullOrWhiteSpace(form.Title))
        {
            AddError(errors, nameof(form.Title), "標題為必填。");
        }
        else if (form.Title.Trim().Length > 100)
        {
            AddError(errors, nameof(form.Title), "標題最多 100 個字元。");
        }

        if (string.IsNullOrWhiteSpace(form.Content))
        {
            AddError(errors, nameof(form.Content), "內容為必填。");
        }
        else if (form.Content.Trim().Length > 1000)
        {
            AddError(errors, nameof(form.Content), "內容最多 1000 個字元。");
        }

        if (!form.ScheduledAt.HasValue)
        {
            AddError(errors, nameof(form.ScheduledAt), "排程時間為必填。");
        }
        else if (_clock.NormalizeMinute(form.ScheduledAt.Value) < _clock.NormalizeMinute(requestTime))
        {
            AddError(errors, nameof(form.ScheduledAt), "排程時間不得早於目前的台灣時間分鐘。");
        }

        if (type == "Personal")
        {
            if (!form.MemberID.HasValue)
            {
                AddError(errors, nameof(form.MemberID), "Personal 通知必須選擇會員。");
            }
            else
            {
                var member = await _repository.GetMemberAsync(form.MemberID.Value, cancellationToken);
                if (member is null || member.IsDeleted)
                {
                    AddError(errors, nameof(form.MemberID), "指定會員不存在或已刪除。");
                }
            }

            if (!string.IsNullOrEmpty(form.TargetRole) || !string.IsNullOrEmpty(form.TargetStatus) || form.TargetLevelID.HasValue)
            {
                AddError(errors, string.Empty, "Personal 通知不得帶入任何條件對象欄位。");
            }
        }
        else if (type == "Condition")
        {
            if (form.MemberID.HasValue)
            {
                AddError(errors, nameof(form.MemberID), "Condition 通知不得指定個人會員。");
            }

            if (!string.IsNullOrEmpty(form.TargetRole) && !_catalog.IsValidRole(form.TargetRole))
            {
                AddError(errors, nameof(form.TargetRole), "指定角色不在允許清單中。");
            }

            if (!string.IsNullOrEmpty(form.TargetStatus) && !_catalog.IsValidStatus(form.TargetStatus))
            {
                AddError(errors, nameof(form.TargetStatus), "指定狀態不在允許清單中。");
            }

            if (form.TargetLevelID.HasValue)
            {
                var level = await _repository.GetLevelAsync(form.TargetLevelID.Value, cancellationToken);
                if (level is null || level.IsDeleted)
                {
                    AddError(errors, nameof(form.TargetLevelID), "指定等級不存在或已刪除。");
                }
            }
        }

        return errors;
    }

    private async Task HydrateOptionsAsync(NotificationFormViewModel form, CancellationToken cancellationToken)
    {
        var members = await _repository.GetActiveMembersAsync(cancellationToken);
        var levels = await _repository.GetActiveLevelsAsync(cancellationToken);
        form.MemberOptions = members.Select(x => new NotificationOptionViewModel(x.MemberID.ToString(), $"{x.MemberID}｜{x.DisplayName}")).ToList();
        form.LevelOptions = levels.Select(x => new NotificationOptionViewModel(x.LevelID.ToString(), x.LevelName)).ToList();
        form.RoleOptions = _catalog.Roles.Select(x => new NotificationOptionViewModel(x.Key, x.Value)).ToList();
        form.StatusOptions = _catalog.Statuses.Select(x => new NotificationOptionViewModel(x.Key, x.Value)).ToList();
    }

    private NotificationIndexRowViewModel ToIndexRow(NotificationReadRecord source) => new()
    {
        NotificationID = source.NotificationID,
        NotificationType = source.NotificationType,
        Title = source.Title,
        AudienceSummary = _presenter.GetAudienceSummary(source),
        ScheduledAt = source.ScheduledAt,
        IsSent = source.IsSent,
        SentAt = source.SentAt,
        CreatedAt = source.CreatedAt,
        CreatedBy = source.CreatedBy,
        IsDeleted = source.IsDeleted
    };

    private NotificationDetailsViewModel ToDetails(NotificationReadRecord source) => new()
    {
        NotificationID = source.NotificationID,
        MemberID = source.MemberID,
        NotificationType = source.NotificationType,
        TargetRole = source.TargetRole,
        TargetStatus = source.TargetStatus,
        TargetLevelID = source.TargetLevelID,
        Title = source.Title,
        Content = source.Content,
        AudienceSummary = _presenter.GetAudienceSummary(source),
        ScheduledAt = source.ScheduledAt,
        SentAt = source.SentAt,
        IsSent = source.IsSent,
        CreatedAt = source.CreatedAt,
        CreatedBy = source.CreatedBy,
        IsDeleted = source.IsDeleted,
        DeletedAt = source.DeletedAt,
        DeletedBy = source.DeletedBy,
        SourceReportID = source.SourceReportID,
        SourceReportOutcome = source.SourceReportOutcome,
        RowVersion = Convert.ToBase64String(source.RowVersion)
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static byte[]? ParseRowVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }
        messages.Add(message);
    }

    private static IReadOnlyDictionary<string, string[]> Freeze(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal);

    private void LogOperation(
        string operation,
        int? notificationId,
        int adminId,
        string classification,
        string correlationId,
        string? notificationType,
        int? memberId)
    {
        if (notificationType == "Personal")
        {
            _logger.LogInformation(
                "Notification operation at {TaipeiTimestamp}; Operation={Operation}; NotificationID={NotificationID}; AdminID={AdminID}; ResultClassification={ResultClassification}; CorrelationID={CorrelationID}; MemberID={MemberID}",
                _clock.GetNow(), operation, notificationId, adminId, classification, correlationId, memberId);
            return;
        }

        _logger.LogInformation(
            "Notification operation at {TaipeiTimestamp}; Operation={Operation}; NotificationID={NotificationID}; AdminID={AdminID}; ResultClassification={ResultClassification}; CorrelationID={CorrelationID}",
            _clock.GetNow(), operation, notificationId, adminId, classification, correlationId);
    }

    private void LogFailure(
        string operation,
        int? notificationId,
        int adminId,
        string correlationId,
        string? notificationType,
        int? memberId,
        Exception exception)
    {
        const string safeSummary = "Notification operation failed.";
        if (notificationType == "Personal")
        {
            _logger.LogError(
                exception,
                "Notification failure at {TaipeiTimestamp}; Operation={Operation}; NotificationID={NotificationID}; AdminID={AdminID}; ResultClassification=Failed; CorrelationID={CorrelationID}; MemberID={MemberID}; ExceptionType={ExceptionType}; SafeSummary={SafeSummary}",
                _clock.GetNow(), operation, notificationId, adminId, correlationId, memberId, exception.GetType().Name, safeSummary);
            return;
        }

        _logger.LogError(
            exception,
            "Notification failure at {TaipeiTimestamp}; Operation={Operation}; NotificationID={NotificationID}; AdminID={AdminID}; ResultClassification=Failed; CorrelationID={CorrelationID}; ExceptionType={ExceptionType}; SafeSummary={SafeSummary}",
            _clock.GetNow(), operation, notificationId, adminId, correlationId, exception.GetType().Name, safeSummary);
    }
}
