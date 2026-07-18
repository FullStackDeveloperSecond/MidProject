using MidProject.Services;

namespace MidProject.Services.IServices;

public interface INotificationAdminAccessEvaluator
{
    Task<NotificationAdminAccessResult> EvaluateAsync(int? memberId, CancellationToken cancellationToken = default);
}

public enum NotificationAdminAccessClassification
{
    Authorized,
    IdentityUnmapped,
    AdministratorUnavailable,
    NotAdministrator
}

public sealed record NotificationAdminAccessResult(
    NotificationAdminAccessClassification Classification,
    NotificationAdminContext? Context = null);
