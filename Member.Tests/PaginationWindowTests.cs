using MidProject.Presentation;

namespace Member.Tests;

public class PaginationWindowTests
{
    [Fact]
    public void GetPageNumbers_WhenNearBeginning_ShowsFirstFivePages()
    {
        Assert.Equal([1, 2, 3, 4, 5], PaginationWindow.GetPageNumbers(1, 80));
    }

    [Fact]
    public void GetPageNumbers_WhenNearMiddle_CentersOnCurrentPage()
    {
        Assert.Equal([38, 39, 40, 41, 42], PaginationWindow.GetPageNumbers(40, 80));
    }

    [Fact]
    public void GetPageNumbers_WhenNearEnd_ShowsLastFivePages()
    {
        Assert.Equal([76, 77, 78, 79, 80], PaginationWindow.GetPageNumbers(80, 80));
    }

    [Fact]
    public void GetPageNumbers_WhenTotalIsSmall_ShowsEveryPage()
    {
        Assert.Equal([1, 2, 3], PaginationWindow.GetPageNumbers(2, 3));
    }
}
