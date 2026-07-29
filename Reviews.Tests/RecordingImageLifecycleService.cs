using MidProject.Services.IServices;

namespace Reviews.Tests;

public sealed class RecordingImageLifecycleService : IImageLifecycleService
{
    public List<int> CleanedImageIds { get; } = [];
    public int? DeletedByMemberId { get; private set; }

    public Task CleanupIfUnreferencedAsync(
        IEnumerable<int> imageIds,
        int deletedByMemberId,
        CancellationToken cancellationToken = default)
    {
        CleanedImageIds.AddRange(imageIds);
        DeletedByMemberId = deletedByMemberId;
        return Task.CompletedTask;
    }
}
