namespace MidProject.Services.IServices;

/// <summary>
/// Consumer contract implemented by the one identity provider selected at startup.
/// The MemberID must be the original ID established by the selected trusted mode.
/// </summary>
public interface ITrustedMemberIdentityAccessor
{
    ValueTask<TrustedMemberIdentity> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public readonly record struct TrustedMemberIdentity(bool IsAuthenticated, int? MemberID);
