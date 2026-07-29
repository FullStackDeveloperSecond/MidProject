using System.Data;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Repositories;

public class TagRepository : ITagRepository
{
    private readonly AppDbContext _db;
    private readonly ITaipeiClock _clock;

    public TagRepository(AppDbContext db, ITaipeiClock clock)
    {
        _db = db;
        _clock = clock;
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
        if (!_db.Database.IsRelational())
        {
            await AddCoreAsync(tag);
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await AddCoreAsync(tag);
        await transaction.CommitAsync();
    }

    private async Task AddCoreAsync(Tag tag)
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
        tag.DeletedAt = tag.IsDeleted ? _clock.GetNow() : null;
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

    public async Task<bool> ReorderAsync(IReadOnlyList<int> orderedIds)
    {
        if (orderedIds.Count == 0 || orderedIds.Distinct().Count() != orderedIds.Count)
        {
            return false;
        }

        var activeTags = await _db.Tags.Where(t => !t.IsDeleted).ToListAsync();
        if (activeTags.Count != orderedIds.Count)
        {
            return false;
        }

        var tagsById = activeTags.ToDictionary(t => t.TagID);
        if (orderedIds.Any(id => !tagsById.ContainsKey(id)))
        {
            return false;
        }

        for (var i = 0; i < orderedIds.Count; i++)
        {
            tagsById[orderedIds[i]].SortOrder = i;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
