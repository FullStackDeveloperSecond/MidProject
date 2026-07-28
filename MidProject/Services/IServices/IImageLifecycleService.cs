namespace MidProject.Services.IServices;

public interface IImageLifecycleService
{
    Task CleanupIfUnreferencedAsync(
        IEnumerable<int> imageIds,
        int deletedByMemberId,
        CancellationToken cancellationToken = default);
}
