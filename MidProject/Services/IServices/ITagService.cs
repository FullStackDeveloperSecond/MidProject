using MidProject.Models.ViewModels.Tags;

namespace MidProject.Services.IServices;

public interface ITagService
{
    Task<TagsIndexViewModel> GetIndexAsync();
    Task<(bool Success, string? Error)> CreateAsync(string name);
    Task ToggleAsync(int id);
}
