using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class ImageLifecycleService : IImageLifecycleService
{
    private readonly AppDbContext _dbContext;
    private readonly IImageUploadService _imageUploadService;
    private readonly ITaipeiClock _clock;
    private readonly ILogger<ImageLifecycleService> _logger;

    public ImageLifecycleService(
        AppDbContext dbContext,
        IImageUploadService imageUploadService,
        ITaipeiClock clock,
        ILogger<ImageLifecycleService> logger)
    {
        _dbContext = dbContext;
        _imageUploadService = imageUploadService;
        _clock = clock;
        _logger = logger;
    }

    public async Task CleanupIfUnreferencedAsync(
        IEnumerable<int> imageIds,
        int deletedByMemberId,
        CancellationToken cancellationToken = default)
    {
        foreach (var imageId in imageIds.Where(id => id > 0).Distinct())
        {
            try
            {
                if (await IsReferencedAsync(imageId, cancellationToken))
                {
                    continue;
                }

                var image = await _dbContext.Images
                    .FirstOrDefaultAsync(item => item.ImageID == imageId, cancellationToken);
                if (image is null)
                {
                    continue;
                }

                image.IsDeleted = true;
                image.DeletedAt = _clock.GetNow();
                image.DeletedBy = deletedByMemberId;
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (!await _imageUploadService.DeleteAsync(image.ImageURL, cancellationToken))
                {
                    _logger.LogWarning(
                        "Unreferenced image file remains after database cleanup at {TaipeiTimestamp}; Operation=CleanupUnreferencedImage; ImageID={ImageID}; ImageUrl={ImageUrl}",
                        _clock.GetNow(),
                        image.ImageID,
                        image.ImageURL);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unreferenced image cleanup failed at {TaipeiTimestamp}; Operation=CleanupUnreferencedImage; ImageID={ImageID}; ExceptionType={ExceptionType}",
                    _clock.GetNow(),
                    imageId,
                    exception.GetType().Name);
            }
        }
    }

    private async Task<bool> IsReferencedAsync(int imageId, CancellationToken cancellationToken) =>
        await _dbContext.RestaurantImages
            .AsNoTracking()
            .AnyAsync(item => item.ImageID == imageId, cancellationToken) ||
        await _dbContext.ReviewImages
            .AsNoTracking()
            .AnyAsync(item => item.ImageID == imageId, cancellationToken) ||
        await _dbContext.Members
            .AsNoTracking()
            .AnyAsync(item => item.AvatarImageID == imageId, cancellationToken) ||
        await _dbContext.AvatarFrames
            .AsNoTracking()
            .AnyAsync(item => item.ImageID == imageId, cancellationToken) ||
        await _dbContext.Reports
            .AsNoTracking()
            .AnyAsync(item => item.ImageID == imageId, cancellationToken);
}
