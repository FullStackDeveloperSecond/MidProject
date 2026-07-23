using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Repositories.IRepositories;

namespace MidProject.Repositories;

public class AvatarFrameRepository : IAvatarFrameRepository
{
    private readonly AppDbContext _db;

    public AvatarFrameRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<AvatarFrame>> GetAllAsync()
    {
        return await _db.AvatarFrames
            .Include(f => f.Image)
            .Where(f => !f.IsDeleted)
            .OrderByDescending(f => f.CreatedAt)
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

    public async Task ToggleActiveAsync(int id)
    {
        var frame = await _db.AvatarFrames.FirstOrDefaultAsync(f => f.FrameID == id && !f.IsDeleted);
        if (frame == null)
        {
            return;
        }

        frame.IsActive = !frame.IsActive;
        frame.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(int id, int byMemberId)
    {
        var frame = await _db.AvatarFrames.FirstOrDefaultAsync(f => f.FrameID == id && !f.IsDeleted);
        if (frame == null)
        {
            return;
        }

        frame.IsActive = false;
        frame.IsDeleted = true;
        frame.DeletedAt = DateTime.Now;
        frame.DeletedBy = byMemberId;
        await _db.SaveChangesAsync();
    }

    public async Task RestoreAsync(int id)
    {
        var frame = await _db.AvatarFrames.FirstOrDefaultAsync(f => f.FrameID == id && f.IsDeleted);
        if (frame == null)
        {
            return;
        }

        frame.IsDeleted = false;
        frame.DeletedAt = null;
        frame.DeletedBy = null;
        frame.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
