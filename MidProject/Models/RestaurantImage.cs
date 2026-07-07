namespace MidProject.Models;

public class RestaurantImage
{
    public int RestaurantID { get; set; }
    public int ImageID { get; set; }

    public Restaurant? Restaurant { get; set; }
    public Image? Image { get; set; }
}
