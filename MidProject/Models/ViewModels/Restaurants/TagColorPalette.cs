namespace MidProject.Models.ViewModels.Restaurants;

// 讓餐廳標籤（台式料理、日式料理、咖啡廳...）依名稱穩定分配到不同的柔和色系，
// 提升可辨識度。用自訂的穩定字串雜湊（而非 string.GetHashCode，其值在
// .NET 每次啟動時會重新隨機化）確保同一個標籤名稱每次都拿到同一種顏色。
public static class TagColorPalette
{
    private const int ColorCount = 8;

    public static string GetColorClass(string? tagName)
    {
        if (string.IsNullOrEmpty(tagName))
        {
            return "ds-tag-c1";
        }

        var hash = 0;
        foreach (var ch in tagName)
        {
            hash = (hash * 31 + ch) & 0x7FFFFFFF;
        }

        return "ds-tag-c" + (hash % ColorCount + 1);
    }
}
