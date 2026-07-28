using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels.PointsStore;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public class AvatarFrameService : IAvatarFrameService
{
    private const int PageSize = 10;
    private static readonly HashSet<string> AllowedRarities = new() { "Common", "Rare", "Limited" };

    private readonly IAvatarFrameRepository _frameRepository;
    private readonly IImageUploadService _imageUploadService;
    private readonly AppDbContext _dbContext;
    private readonly ITaipeiClock _clock;

    public AvatarFrameService(
        IAvatarFrameRepository frameRepository,
        IImageUploadService imageUploadService,
        AppDbContext dbContext,
        ITaipeiClock clock)
    {
        _frameRepository = frameRepository;
        _imageUploadService = imageUploadService;
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<AvatarFramesIndexViewModel> GetIndexAsync(
        string? keyword, string? rarity, bool? isActive, string? sortBy, int page)
    {
        var framesQuery = _dbContext.AvatarFrames
            .Include(f => f.Image)
            .Where(f => !f.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
            framesQuery = framesQuery.Where(f => f.Name.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(rarity))
            framesQuery = framesQuery.Where(f => f.Rarity == rarity);
        if (isActive.HasValue)
            framesQuery = framesQuery.Where(f => f.IsActive == isActive.Value);

        var resolvedSort = sortBy is "priceAsc" or "priceDesc" or "name" ? sortBy : "newest";
        framesQuery = resolvedSort switch
        {
            "priceAsc" => framesQuery.OrderBy(f => f.PointsPrice),
            "priceDesc" => framesQuery.OrderByDescending(f => f.PointsPrice),
            "name" => framesQuery.OrderBy(f => f.Name),
            _ => framesQuery.OrderByDescending(f => f.CreatedAt)
        };

        var totalItems = await framesQuery.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);
        var frames = await framesQuery
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
        var redemptionCounts = await _frameRepository.GetRedemptionCountsAsync();

        var now = _clock.GetNow();
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthlyQuery = _dbContext.PointsTransactions
            .Where(t => t.Type == "Redeem" && t.CreatedAt >= monthStart);
        var monthlyRedemptionCount = await monthlyQuery.CountAsync();
        var monthlyPointsSpent = -(await monthlyQuery.SumAsync(t => t.Amount));

        return new AvatarFramesIndexViewModel
        {
            TotalCount = await _dbContext.AvatarFrames.CountAsync(f => !f.IsDeleted),
            ActiveCount = await _dbContext.AvatarFrames.CountAsync(f => !f.IsDeleted && f.IsActive),
            AveragePointsPrice = await _dbContext.AvatarFrames.Where(f => !f.IsDeleted).Select(f => (double?)f.PointsPrice).AverageAsync() ?? 0,
            MonthlyRedemptionCount = monthlyRedemptionCount,
            MonthlyPointsSpent = monthlyPointsSpent,
            Frames = frames.Select(f => new AvatarFrameRowViewModel
            {
                FrameID = f.FrameID,
                Name = f.Name,
                Rarity = f.Rarity,
                PointsPrice = f.PointsPrice,
                IsActive = f.IsActive,
                ImageUrl = f.Image?.ImageURL,
                RedeemedCount = redemptionCounts.TryGetValue(f.FrameID, out var count) ? count : 0
            }).ToList(),
            Keyword = keyword,
            Rarity = rarity,
            IsActive = isActive,
            SortBy = resolvedSort,
            CurrentPage = currentPage,
            TotalItems = totalItems,
            PageSize = PageSize
        };
    }

    public async Task<AvatarFramesDeletedIndexViewModel> GetDeletedIndexAsync(
        string? keyword, string? rarity, string? sortBy, int page)
    {
        var framesQuery = _dbContext.AvatarFrames
            .Include(f => f.Image)
            .Include(f => f.DeletedByMember)
            .Where(f => f.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
            framesQuery = framesQuery.Where(f => f.Name.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(rarity))
            framesQuery = framesQuery.Where(f => f.Rarity == rarity);

        var resolvedSort = sortBy is "name" or "price" ? sortBy : "deletedAt";
        framesQuery = resolvedSort switch
        {
            "name" => framesQuery.OrderBy(f => f.Name),
            "price" => framesQuery.OrderByDescending(f => f.PointsPrice),
            _ => framesQuery.OrderByDescending(f => f.DeletedAt)
        };

        var totalItems = await framesQuery.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);
        var frames = await framesQuery.Skip((currentPage - 1) * PageSize).Take(PageSize).ToListAsync();

        return new AvatarFramesDeletedIndexViewModel
        {
            TotalDeletedCount = await _dbContext.AvatarFrames.CountAsync(f => f.IsDeleted),
            MostRecentDeletedAt = await _dbContext.AvatarFrames.Where(f => f.IsDeleted).MaxAsync(f => f.DeletedAt),
            Frames = frames.Select(f => new AvatarFrameDeletedRowViewModel
            {
                FrameID = f.FrameID,
                Name = f.Name,
                Rarity = f.Rarity,
                PointsPrice = f.PointsPrice,
                ImageUrl = f.Image?.ImageURL,
                DeletedAt = f.DeletedAt,
                DeletedByName = f.DeletedByMember?.NickName ?? f.DeletedByMember?.UserName
            }).ToList(),
            Keyword = keyword,
            Rarity = rarity,
            SortBy = resolvedSort,
            CurrentPage = currentPage,
            TotalItems = totalItems,
            PageSize = PageSize
        };
    }

    public async Task<AvatarFrameFormViewModel?> GetForEditAsync(int id)
    {
        var frame = await _frameRepository.GetByIdAsync(id);
        if (frame == null)
        {
            return null;
        }

        return new AvatarFrameFormViewModel
        {
            FrameID = frame.FrameID,
            Name = frame.Name,
            Description = frame.Description,
            Rarity = frame.Rarity,
            PointsPrice = frame.PointsPrice,
            SortOrder = frame.SortOrder,
            IsActive = frame.IsActive,
            ExistingImageId = frame.ImageID,
            ExistingImageUrl = frame.Image?.ImageURL
        };
    }

    public async Task<(bool Success, string? Error)> CreateAsync(AvatarFrameFormViewModel form, int adminId)
    {
        if (!AllowedRarities.Contains(form.Rarity))
        {
            return (false, "分類只能是 Common、Rare 或 Limited。");
        }

        if (form.ImageFile == null)
        {
            return (false, "請上傳外框圖片。");
        }

        Image image;
        try
        {
            image = await _imageUploadService.SaveAsync(form.ImageFile, "AvatarFrame", adminId);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }

        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                _dbContext.Images.Add(image);
                await _dbContext.SaveChangesAsync();

                var now = _clock.GetNow();
                await _frameRepository.AddAsync(new AvatarFrame
                {
                    Name = form.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
                    Rarity = form.Rarity,
                    PointsPrice = form.PointsPrice,
                    SortOrder = form.SortOrder,
                    IsActive = form.IsActive,
                    ImageID = image.ImageID,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch
        {
            // DB 交易無法建立或交易內容失敗時，清除已經寫入硬碟的檔案，
            // 避免留下資料庫裡完全沒有紀錄指向的孤兒檔案。
            await _imageUploadService.DeleteAsync(image.ImageURL);
            return (false, "新增失敗，請重試。");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, AvatarFrameFormViewModel form, int adminId)
    {
        if (!AllowedRarities.Contains(form.Rarity))
        {
            return (false, "分類只能是 Common、Rare 或 Limited。");
        }

        var frame = await _frameRepository.GetByIdAsync(id);
        if (frame == null)
        {
            return (false, "找不到這個商品。");
        }

        // 圖片實際寫入硬碟這一步沒辦法被 DB 交易保護，所以先做，並記住檔案路徑，
        // 讓下面的交易失敗時可以呼叫 DeleteAsync 手動清掉，不留孤兒檔案。
        Image? newImage = null;
        if (form.ImageFile != null)
        {
            try
            {
                newImage = await _imageUploadService.SaveAsync(form.ImageFile, "AvatarFrame", adminId);
            }
            catch (InvalidOperationException ex)
            {
                return (false, ex.Message);
            }
        }

        var oldImageId = frame.ImageID;
        var now = _clock.GetNow();

        // 商品欄位更新、新圖片寫入 DB、舊圖片軟刪除，三步驟包在同一個交易裡：
        // 要嘛全部成功，要嘛全部回滾，不會出現「Frame 已經指向新圖但舊圖沒被標記刪除」
        // 或「新圖片資料寫進去了但 Frame 欄位沒更新」這種中間狀態。
        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                frame.Name = form.Name.Trim();
                frame.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
                frame.Rarity = form.Rarity;
                frame.PointsPrice = form.PointsPrice;
                frame.SortOrder = form.SortOrder;
                frame.IsActive = form.IsActive;
                frame.UpdatedAt = now;

                if (newImage != null)
                {
                    _dbContext.Images.Add(newImage);
                    await _dbContext.SaveChangesAsync();
                    frame.ImageID = newImage.ImageID;
                }

                await _frameRepository.SaveChangesAsync();

                if (newImage != null && oldImageId.HasValue)
                {
                    var oldImage = await _dbContext.Images.FindAsync(oldImageId.Value);
                    if (oldImage != null)
                    {
                        oldImage.IsDeleted = true;
                        oldImage.DeletedAt = now;
                        oldImage.DeletedBy = adminId;
                        await _dbContext.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch
        {
            if (newImage != null)
            {
                await _imageUploadService.DeleteAsync(newImage.ImageURL);
            }
            return (false, "更新失敗，請重試。");
        }

        return (true, null);
    }

    public Task<bool> ToggleActiveAsync(int id) => _frameRepository.ToggleActiveAsync(id);

    public Task<bool> DeleteAsync(int id, int adminId) => _frameRepository.SoftDeleteAsync(id, adminId);

    public Task<bool> RestoreAsync(int id) => _frameRepository.RestoreAsync(id);
}
