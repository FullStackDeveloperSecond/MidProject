using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class DevelopmentTemporaryTrustedMemberIdentityAccessor : ITrustedMemberIdentityAccessor
{
    private readonly object _sync = new();
    private int? _sealedMemberId;

    internal void Seal(int memberId)
    {
        if (memberId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(memberId));
        }

        lock (_sync)
        {
            if (_sealedMemberId.HasValue)
            {
                throw new InvalidOperationException("The temporary notification identity is already sealed.");
            }

            _sealedMemberId = memberId;
        }
    }

    public ValueTask<TrustedMemberIdentity> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_sealedMemberId.HasValue)
            {
                throw new InvalidOperationException(
                    "The temporary notification identity has not completed startup validation.");
            }

            return ValueTask.FromResult(new TrustedMemberIdentity(true, _sealedMemberId.Value));
        }
    }
}
