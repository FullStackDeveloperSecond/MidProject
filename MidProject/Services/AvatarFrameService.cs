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
    private readonly IAvatarFrameRepository _frameRepository;
    private readonly AppDbContext _dbContext;

    public AvatarFrameService(
        IAvatarFrameRepository frameRepository,
        AppDbContext dbContext)
    {
        _frameRepository = frameRepository;
        _dbContext = dbContext;
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

        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var monthlyRedemptions = await _dbContext.PointsTransactions
            .Where(t => t.Type == "Redeem" && t.CreatedAt >= monthStart)
            .ToListAsync();

        return new AvatarFramesIndexViewModel
        {
            TotalCount = await _dbContext.AvatarFrames.CountAsync(f => !f.IsDeleted),
            ActiveCount = await _dbContext.AvatarFrames.CountAsync(f => !f.IsDeleted && f.IsActive),
            AveragePointsPrice = await _dbContext.AvatarFrames.Where(f => !f.IsDeleted).Select(f => (double?)f.PointsPrice).AverageAsync() ?? 0,
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
            IsActive = frame.IsActive
        };
    }

    public async Task<(bool Success, string? Error)> CreateAsync(AvatarFrameFormViewModel form, int adminId)
    {
        await _frameRepository.AddAsync(new AvatarFrame
        {
            Name = form.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
            Rarity = form.Rarity,
            PointsPrice = form.PointsPrice,
            IsActive = form.IsActive
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

        await _frameRepository.SaveChangesAsync();
        return (true, null);
    }

    public Task ToggleActiveAsync(int id) => _frameRepository.ToggleActiveAsync(id);

    public Task DeleteAsync(int id, int adminId) => _frameRepository.SoftDeleteAsync(id, adminId);

    public Task RestoreAsync(int id) => _frameRepository.RestoreAsync(id);
}
