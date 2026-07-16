namespace MidProject.Models.ViewModels.Tags;

public class TagCardViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public bool IsDeleted { get; set; }
}
