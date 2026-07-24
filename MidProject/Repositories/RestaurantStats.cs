namespace MidProject.Repositories;

public class RestaurantStats
{
    public int Total { get; set; }
    public decimal AvgRating { get; set; }
    public int ReviewCount { get; set; }
    public int DisabledCount { get; set; }
}

public class RestaurantReviewStats
{
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
}
