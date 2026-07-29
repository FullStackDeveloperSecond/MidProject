using MidProject.Services;

namespace MidProject.Services.IServices;

public interface IAdminAccessEvaluator
{
    Task<AdminAccessResult> EvaluateAsync(int? memberId, CancellationToken cancellationToken = default);
}

public enum AdminAccessClassification
{
    Authorized,
    IdentityUnmapped,
    AdministratorUnavailable,
    NotAdministrator
}

public sealed record AdminAccessResult(
    AdminAccessClassification Classification,
    AdminContext? Context = null);
