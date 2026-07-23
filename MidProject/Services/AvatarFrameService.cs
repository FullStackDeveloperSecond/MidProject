using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels.PointsStore;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public class AvatarFrameService : IAvatarFrameService
{
    private readonly IAvatarFrameRepository _frameRepository;
    private readonly IImageUploadService _imageUploadService;
    private readonly AppDbContext _dbContext;

    public AvatarFrameService(
        IAvatarFrameRepository frameRepository,
        IImageUploadService imageUploadService,
        AppDbContext dbContext)
    {
        _frameRepository = frameRepository;
        _imageUploadService = imageUploadService;
        _dbContext = dbContext;
    }

    public async Task<AvatarFramesIndexViewModel> GetIndexAsync()
    {
        var frames = await _frameRepository.GetAllAsync();
        var redemptionCounts = await _frameRepository.GetRedemptionCountsAsync();

        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var monthlyRedemptions = await _dbContext.PointsTransactions
            .Where(t => t.Type == "Redeem" && t.CreatedAt >= monthStart)
            .ToListAsync();

        return new AvatarFramesIndexViewModel
        {
            TotalCount = frames.Count,
            ActiveCount = frames.Count(f => f.IsActive),
            AveragePointsPrice = frames.Count > 0 ? frames.Average(f => f.PointsPrice) : 0,
            MonthlyRedemptionCount = monthlyRedemptions.Count,
            MonthlyPointsSpent = -monthlyRedemptions.Sum(t => t.Amount),
            Frames = frames.Select(f => new AvatarFrameRowViewModel
            {
                FrameID = f.FrameID,
                Name = f.Name,
                Rarity = f.Rarity,
                PointsPrice = f.PointsPrice,
                IsActive = f.IsActive,
                ImageUrl = f.Image?.ImageURL,
                RedeemedCount = redemptionCounts.TryGetValue(f.FrameID, out var count) ? count : 0
            }).ToList()
        };
    }

    public async Task<List<AvatarFrameDeletedRowViewModel>> GetDeletedIndexAsync()
    {
        var frames = await _frameRepository.GetDeletedAsync();

        return frames.Select(f => new AvatarFrameDeletedRowViewModel
        {
            FrameID = f.FrameID,
            Name = f.Name,
            Rarity = f.Rarity,
            PointsPrice = f.PointsPrice,
            ImageUrl = f.Image?.ImageURL,
            DeletedAt = f.DeletedAt,
            DeletedByName = f.DeletedByMember?.NickName ?? f.DeletedByMember?.UserName
        }).ToList();
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
            IsActive = frame.IsActive,
            ExistingImageId = frame.ImageID,
            ExistingImageUrl = frame.Image?.ImageURL
        };
    }

    public async Task<(bool Success, string? Error)> CreateAsync(AvatarFrameFormViewModel form, int adminId)
    {
        if (form.ImageFile == null)
        {
            return (false, "請上傳外框圖片。");
        }

        if (!IsPng(form.ImageFile))
        {
            return (false, "外框圖片僅接受 PNG 格式（外框需要透明背景，才能疊在會員頭像上）。");
        }

        var image = await _imageUploadService.SaveAsync(form.ImageFile, "AvatarFrame", adminId);
        _dbContext.Images.Add(image);
        await _dbContext.SaveChangesAsync();

        await _frameRepository.AddAsync(new AvatarFrame
        {
            Name = form.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
            Rarity = form.Rarity,
            PointsPrice = form.PointsPrice,
            IsActive = form.IsActive,
            ImageID = image.ImageID
        });

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, AvatarFrameFormViewModel form, int adminId)
    {
        var frame = await _frameRepository.GetByIdAsync(id);
        if (frame == null)
        {
            return (false, "找不到這個商品。");
        }

        frame.Name = form.Name.Trim();
        frame.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        frame.Rarity = form.Rarity;
        frame.PointsPrice = form.PointsPrice;
        frame.IsActive = form.IsActive;
        frame.UpdatedAt = DateTime.Now;

        if (form.ImageFile != null)
        {
            if (!IsPng(form.ImageFile))
            {
                return (false, "外框圖片僅接受 PNG 格式（外框需要透明背景，才能疊在會員頭像上）。");
            }

            var image = await _imageUploadService.SaveAsync(form.ImageFile, "AvatarFrame", adminId);
            _dbContext.Images.Add(image);
            await _dbContext.SaveChangesAsync();
            frame.ImageID = image.ImageID;
        }

        await _frameRepository.SaveChangesAsync();
        return (true, null);
    }

    private static bool IsPng(IFormFile file) =>
        string.Equals(Path.GetExtension(file.FileName), ".png", StringComparison.OrdinalIgnoreCase);

    public Task ToggleActiveAsync(int id) => _frameRepository.ToggleActiveAsync(id);

    public Task DeleteAsync(int id, int adminId) => _frameRepository.SoftDeleteAsync(id, adminId);

    public Task RestoreAsync(int id) => _frameRepository.RestoreAsync(id);
}
