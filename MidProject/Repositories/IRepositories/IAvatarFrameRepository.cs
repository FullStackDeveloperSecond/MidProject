using MidProject.Models;

namespace MidProject.Repositories.IRepositories;

public interface IAvatarFrameRepository
{
    Task<List<AvatarFrame>> GetAllAsync();
    Task<AvatarFrame?> GetByIdAsync(int id);
    Task<Dictionary<int, int>> GetRedemptionCountsAsync();
    Task AddAsync(AvatarFrame frame);
    Task ToggleActiveAsync(int id);
    Task SoftDeleteAsync(int id, int byMemberId);
    Task SaveChangesAsync();
}
