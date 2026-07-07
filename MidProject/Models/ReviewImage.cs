namespace MidProject.Models;

public class ReviewImage
{
    public int ReviewID { get; set; }
    public int ImageID { get; set; }

    public Review? Review { get; set; }
    public Image? Image { get; set; }
}
