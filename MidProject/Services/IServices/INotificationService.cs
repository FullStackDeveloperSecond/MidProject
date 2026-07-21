using MidProject.Models.ViewModels.Notifications;

namespace MidProject.Services.IServices;

public interface INotificationService
{
    Task<NotificationIndexViewModel> GetIndexAsync(NotificationIndexQuery query, CancellationToken cancellationToken = default);
    Task<NotificationDetailsViewModel?> GetDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<NotificationFormViewModel> GetCreateFormAsync(NotificationFormViewModel? submitted = null, CancellationToken cancellationToken = default);
    Task<NotificationFormViewModel?> GetEditFormAsync(int id, NotificationFormViewModel? submitted = null, CancellationToken cancellationToken = default);
    Task<NotificationDeleteViewModel?> GetDeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<NotificationCommandResult> CreateAsync(NotificationFormViewModel form, int adminId, string correlationId, CancellationToken cancellationToken = default);
    Task<NotificationCommandResult> EditAsync(int id, NotificationFormViewModel form, int adminId, string correlationId, CancellationToken cancellationToken = default);
    Task<NotificationCommandResult> DeleteAsync(int id, string rowVersion, int adminId, string correlationId, CancellationToken cancellationToken = default);
    Task<SingleSendResult> SendAsync(int id, int adminId, string correlationId, CancellationToken cancellationToken = default);
    Task<BatchSendResult> SendDueAsync(int adminId, string correlationId, CancellationToken cancellationToken = default);
}

public enum NotificationCommandClassification
{
    Success,
    ValidationFailed,
    NotFound,
    Deleted,
    ReadOnly,
    ConcurrentlyHandled,
    Failed
}

public sealed record NotificationCommandResult(
    NotificationCommandClassification Classification,
    int? NotificationID = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public enum SingleSendClassification
{
    Success,
    NotFound,
    Deleted,
    AlreadySent,
    NotDue,
    ConcurrentlyHandled,
    Failed
}

public sealed record SingleSendResult(SingleSendClassification Classification, int NotificationID);

public enum BatchItemClassification
{
    Success,
    AlreadySent,
    NotDue,
    Deleted,
    Failed
}

public sealed record BatchSendItemResult(int NotificationID, BatchItemClassification Classification, string? SafeErrorCode = null);

public sealed class BatchSendResult
{
    public required IReadOnlyList<BatchSendItemResult> Items { get; init; }
    public required string CorrelationID { get; init; }
    public int ProcessedCount => Items.Count;
    public int SuccessCount => Items.Count(x => x.Classification == BatchItemClassification.Success);
    public int AlreadySentCount => Items.Count(x => x.Classification == BatchItemClassification.AlreadySent);
    public int NotDueCount => Items.Count(x => x.Classification == BatchItemClassification.NotDue);
    public int DeletedCount => Items.Count(x => x.Classification == BatchItemClassification.Deleted);
    public int FailedCount => Items.Count(x => x.Classification == BatchItemClassification.Failed);
}
