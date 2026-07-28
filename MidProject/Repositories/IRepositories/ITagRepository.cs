using MidProject.Models;

namespace MidProject.Repositories.IRepositories;

public interface ITagRepository
{
    Task<List<Tag>> GetAllAsync();
    Task<Tag?> FindByNameAsync(string name);
    Task<Tag?> GetByIdAsync(int id);
    Task AddAsync(Tag tag);
    Task ToggleAsync(int id, int byMemberId);
    Task<Dictionary<int, int>> GetActiveUsageCountsAsync();
    Task ReorderAsync(List<int> orderedIds);
    Task SaveChangesAsync();
}
