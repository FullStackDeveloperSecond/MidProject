namespace MidProject.Services.IServices;

/// <summary>
/// Consumer contract implemented by the externally owned Account/Login integration.
/// The MemberID must be the original ID of the successfully authenticated Member row.
/// </summary>
public interface ITrustedMemberIdentityAccessor
{
    ValueTask<TrustedMemberIdentity> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public readonly record struct TrustedMemberIdentity(bool IsAuthenticated, int? MemberID);
