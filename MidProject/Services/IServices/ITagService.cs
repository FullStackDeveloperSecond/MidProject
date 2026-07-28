using MidProject.Models.ViewModels.Tags;

namespace MidProject.Services.IServices;

public interface ITagService
{
    Task<TagsIndexViewModel> GetIndexAsync();
    Task<(bool Success, string? Error)> CreateAsync(string name, int adminId);
    Task<bool> ToggleAsync(int id, int adminId);
    Task ReorderAsync(List<int> orderedIds);
}
