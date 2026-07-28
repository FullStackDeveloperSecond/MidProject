using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Repositories.IRepositories;

namespace MidProject.Repositories;

public class TagRepository : ITagRepository
{
    private readonly AppDbContext _db;

    public TagRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Tag>> GetAllAsync()
    {
        return await _db.Tags.OrderBy(t => t.SortOrder).ThenBy(t => t.TagName).ToListAsync();
    }

    public Task<Tag?> FindByNameAsync(string name)
    {
        return _db.Tags.FirstOrDefaultAsync(t => t.TagName == name);
    }

    public Task<Tag?> GetByIdAsync(int id)
    {
        return _db.Tags.FirstOrDefaultAsync(t => t.TagID == id);
    }

    public async Task AddAsync(Tag tag)
    {
        var maxSortOrder = await _db.Tags.Select(t => (int?)t.SortOrder).MaxAsync() ?? -1;
        tag.SortOrder = maxSortOrder + 1;
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
    }

    public async Task ToggleAsync(int id, int byMemberId)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.TagID == id);
        if (tag == null)
        {
            return;
        }

        tag.IsDeleted = !tag.IsDeleted;
        tag.DeletedAt = tag.IsDeleted ? DateTime.Now : null;
        tag.DeletedBy = tag.IsDeleted ? byMemberId : null;
        await _db.SaveChangesAsync();
    }

    public async Task<Dictionary<int, int>> GetActiveUsageCountsAsync()
    {
        return await _db.RestaurantTags
            .Where(rt => rt.Restaurant != null && !rt.Restaurant.IsDeleted)
            .GroupBy(rt => rt.TagID)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TagId, x => x.Count);
    }

    // 拖曳排序只重新編號目前「使用中」的標籤（停用標籤的 SortOrder 維持不動，
    // 停用區塊本來就不能拖曳，也不參與這份清單），依前端送來的新順序逐一寫回。
    public async Task ReorderAsync(List<int> orderedIds)
    {
        var tags = await _db.Tags.Where(t => orderedIds.Contains(t.TagID)).ToListAsync();
        var tagsById = tags.ToDictionary(t => t.TagID);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (tagsById.TryGetValue(orderedIds[i], out var tag))
            {
                tag.SortOrder = i;
            }
        }

        await _db.SaveChangesAsync();
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
