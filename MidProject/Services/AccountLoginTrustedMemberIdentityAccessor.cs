using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class AccountLoginTrustedMemberIdentityAccessor : ITrustedMemberIdentityAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountLoginTrustedMemberIdentityAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask<TrustedMemberIdentity> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cookieIdentity = _httpContextAccessor.HttpContext?.User.Identities
            .FirstOrDefault(identity =>
                identity.IsAuthenticated &&
                string.Equals(
                    identity.AuthenticationType,
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    StringComparison.Ordinal));

        if (cookieIdentity is null)
        {
            return ValueTask.FromResult(new TrustedMemberIdentity(false, null));
        }

        var memberIdValue = cookieIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var memberId = int.TryParse(
            memberIdValue,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedMemberId) &&
            parsedMemberId > 0
                ? parsedMemberId
                : (int?)null;

        return ValueTask.FromResult(new TrustedMemberIdentity(true, memberId));
    }
}
