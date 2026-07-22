using MidProject.Models;
using MidProject.Models.ViewModels.Tags;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public class TagService : ITagService
{
    private readonly ITagRepository _tagRepository;
    private readonly IRestaurantRepository _restaurantRepository;

    public TagService(ITagRepository tagRepository, IRestaurantRepository restaurantRepository)
    {
        _tagRepository = tagRepository;
        _restaurantRepository = restaurantRepository;
    }

    public async Task<TagsIndexViewModel> GetIndexAsync()
    {
        var tags = await _tagRepository.GetAllAsync();
        var usage = await _tagRepository.GetActiveUsageCountsAsync();

        var cards = tags.Select(t => new TagCardViewModel
        {
            Id = t.TagID,
            Name = t.TagName,
            IsDeleted = t.IsDeleted,
            UsageCount = usage.TryGetValue(t.TagID, out var count) ? count : 0
        }).ToList();

        return new TagsIndexViewModel
        {
            ActiveTags = cards.Where(c => !c.IsDeleted).ToList(),
            InactiveTags = cards.Where(c => c.IsDeleted).ToList()
        };
    }

    public async Task<(bool Success, string? Error)> CreateAsync(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return (false, "請輸入標籤名稱。");
        }

        var existing = await _tagRepository.FindByNameAsync(trimmed);
        if (existing != null)
        {
            if (existing.IsDeleted)
            {
                var adminId = await _restaurantRepository.GetDefaultAdminMemberIdAsync();
                await _tagRepository.ToggleAsync(existing.TagID, adminId);
            }

            return (true, null);
        }

        await _tagRepository.AddAsync(new Tag { TagName = trimmed });
        return (true, null);
    }

    public async Task ToggleAsync(int id)
    {
        var adminId = await _restaurantRepository.GetDefaultAdminMemberIdAsync();
        await _tagRepository.ToggleAsync(id, adminId);
    }
}
