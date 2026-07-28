using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Repositories;

public class AvatarFrameRepository : IAvatarFrameRepository
{
    private readonly AppDbContext _db;
    private readonly ITaipeiClock _clock;

    public AvatarFrameRepository(AppDbContext db, ITaipeiClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<AvatarFrame>> GetAllAsync()
    {
        return await _db.AvatarFrames
            .Include(f => f.Image)
            .Where(f => !f.IsDeleted)
            .OrderBy(f => f.SortOrder)
            .ThenByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AvatarFrame>> GetDeletedAsync()
    {
        return await _db.AvatarFrames
            .Include(f => f.Image)
            .Include(f => f.DeletedByMember)
            .Where(f => f.IsDeleted)
            .OrderByDescending(f => f.DeletedAt)
            .ToListAsync();
    }

    public Task<AvatarFrame?> GetByIdAsync(int id)
    {
        return _db.AvatarFrames
            .Include(f => f.Image)
            .FirstOrDefaultAsync(f => f.FrameID == id && !f.IsDeleted);
    }

    public async Task<Dictionary<int, int>> GetRedemptionCountsAsync()
    {
        return await _db.MemberAvatarFrames
            .GroupBy(m => m.FrameID)
            .Select(g => new { FrameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FrameId, x => x.Count);
    }

    public async Task AddAsync(AvatarFrame frame)
    {
        _db.AvatarFrames.Add(frame);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ToggleActiveAsync(int id)
    {
        var frame = await _db.AvatarFrames.FirstOrDefaultAsync(f => f.FrameID == id && !f.IsDeleted);
        if (frame == null)
        {
            return false;
        }

        frame.IsActive = !frame.IsActive;
        frame.UpdatedAt = _clock.GetNow();
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SoftDeleteAsync(int id, int byMemberId)
    {
        var frame = await _db.AvatarFrames.FirstOrDefaultAsync(f => f.FrameID == id && !f.IsDeleted);
        if (frame == null)
        {
            return false;
        }

        frame.IsActive = false;
        frame.IsDeleted = true;
        frame.DeletedAt = _clock.GetNow();
        frame.DeletedBy = byMemberId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(int id)
    {
        var frame = await _db.AvatarFrames.FirstOrDefaultAsync(f => f.FrameID == id && f.IsDeleted);
        if (frame == null)
        {
            return false;
        }

        frame.IsDeleted = false;
        frame.DeletedAt = null;
        frame.DeletedBy = null;
        frame.UpdatedAt = _clock.GetNow();
        await _db.SaveChangesAsync();
        return true;
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
