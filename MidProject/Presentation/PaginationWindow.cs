namespace MidProject.Presentation;

public static class PaginationWindow
{
    public static IReadOnlyList<int> GetPageNumbers(
        int currentPage,
        int totalPages,
        int maximumVisiblePages = 5)
    {
        if (totalPages <= 0 || maximumVisiblePages <= 0)
        {
            return [];
        }

        currentPage = Math.Clamp(currentPage, 1, totalPages);
        var visibleCount = Math.Min(maximumVisiblePages, totalPages);
        var startPage = Math.Max(1, currentPage - visibleCount / 2);
        var endPage = Math.Min(totalPages, startPage + visibleCount - 1);
        startPage = Math.Max(1, endPage - visibleCount + 1);

        return Enumerable.Range(startPage, endPage - startPage + 1).ToArray();
    }
}
