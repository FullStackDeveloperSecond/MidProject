using MidProject.Models;

namespace MidProject.Repositories.IRepositories;

public interface IAvatarFrameRepository
{
    Task<List<AvatarFrame>> GetAllAsync();
    Task<List<AvatarFrame>> GetDeletedAsync();
    Task<AvatarFrame?> GetByIdAsync(int id);
    Task<Dictionary<int, int>> GetRedemptionCountsAsync();
    Task AddAsync(AvatarFrame frame);
    Task<bool> ToggleActiveAsync(int id);
    Task<bool> SoftDeleteAsync(int id, int byMemberId);
    Task<bool> RestoreAsync(int id);
    Task SaveChangesAsync();
}
