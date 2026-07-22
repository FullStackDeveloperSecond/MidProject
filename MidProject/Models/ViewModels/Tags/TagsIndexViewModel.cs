namespace MidProject.Models.ViewModels.Tags;

public class TagsIndexViewModel
{
    public List<TagCardViewModel> ActiveTags { get; set; } = new();
    public List<TagCardViewModel> InactiveTags { get; set; } = new();
}
