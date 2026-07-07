namespace MidProject.Models;

public class RestaurantTag
{
    public int RestaurantID { get; set; }
    public int TagID { get; set; }

    public Restaurant? Restaurant { get; set; }
    public Tag? Tag { get; set; }
}
