using MidProject.Models;

namespace MidProject.Models.ViewModels
{
    /// <summary>
    /// 對應原型 index.html 整個列表頁需要的資料。
    /// </summary>
    public class ReviewListViewModel
    {
        public List<Review> Items { get; set; } = new();

        // 目前的篩選條件（畫面上要記得把 Tab / 搜尋框 / 下拉選單設回這些值）
        public string Tab { get; set; } = "all";
        public string? Search { get; set; }
        public int? Rating { get; set; }
        public string Time { get; set; } = "all";
        public string SortBy { get; set; } = "time";  // time / rating / report
        public string SortDir { get; set; } = "desc";  // desc=大到小/新到舊, asc=反過來

        // 分頁資訊
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }

        // 三格統計卡片（規格書 4.1）
        public int NormalCount { get; set; }
        public int PendingCount { get; set; }
        public int DeletedCount { get; set; }
    }

    /// <summary>
    /// 對應原型 detail.html 整個詳情頁需要的資料。
    /// </summary>
    public class ReviewDetailViewModel
    {
        public Review Review { get; set; } = null!;
        public List<Report> Reports { get; set; } = new();
    }
}
