using System.Text;
using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Models.ViewModels.Tags;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public class TagService : ITagService
{
    private readonly ITagRepository _tagRepository;

    public TagService(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
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

    public async Task<(bool Success, string? Error)> CreateAsync(string name, int adminId)
    {
        var trimmed = (name ?? string.Empty).Trim().Normalize(NormalizationForm.FormKC);
        if (string.IsNullOrEmpty(trimmed))
        {
            return (false, "請輸入標籤名稱。");
        }
        if (trimmed.Length > 50)
        {
            return (false, "標籤名稱最多 50 個字。");
        }

        var existing = await _tagRepository.FindByNameAsync(trimmed);
        if (existing != null)
        {
            if (existing.IsDeleted)
            {
                await _tagRepository.ToggleAsync(existing.TagID, adminId);
            }

            return (true, null);
        }

        try
        {
            await _tagRepository.AddAsync(new Tag { TagName = trimmed });
            return (true, null);
        }
        catch (DbUpdateException)
        {
            // 另一個請求可能在名稱查重後先完成新增；以安全訊息結束，不把唯一鍵例外回傳成 500。
            return (false, "此標籤已存在，請重新整理後確認。");
        }
    }

    public async Task<bool> ToggleAsync(int id, int adminId)
    {
        var tag = await _tagRepository.GetByIdAsync(id);
        if (tag == null) return false;
        await _tagRepository.ToggleAsync(id, adminId);
        return true;
    }

    public Task<bool> ReorderAsync(IReadOnlyList<int> orderedIds) => _tagRepository.ReorderAsync(orderedIds);
}
