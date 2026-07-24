namespace MidProject.Helpers
{
    /// <summary>
    /// 評論管理頁面共用的顯示邏輯（狀態文字、相對時間、大頭貼顏色等）。
    /// 對應原型 data.js 裡的 statusDisplay() / formatRelative() / avatarStyle() 等函式，
    /// 從 JS 搬到 C# 給 Razor View 用。
    /// </summary>
    public static class ReviewDisplayHelpers
    {
        public static (string Label, string CssClass) GetStatusPill(bool isDeleted, string status)
        {
            if (isDeleted)
            {
                return ("已刪除", "pill-deleted");
            }
            return status == "Active" ? ("正常", "pill-normal") : ("審核中", "pill-pending");
        }

        public static (string Label, string CssClass) GetReportStatusLabel(string status) => status switch
        {
            "Pending" => ("待處理", "rs-pending"),
            "Approved" => ("檢舉成立", "rs-approved"),
            "Rejected" => ("駁回檢舉", "rs-rejected"),
            _ => (status, "rs-pending"),
        };

        public static string FormatRelative(DateTime dt)
        {
            var diff = DateTime.Now - dt;
            if (diff.TotalMinutes < 1) return "剛剛";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} 分鐘前";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} 小時前";

            var days = (int)diff.TotalDays;
            if (days == 1) return "昨天";
            if (days < 7) return $"{days} 天前";
            return dt.ToString("M/d");
        }

        public static string FormatFull(DateTime dt) => dt.ToString("yyyy/MM/dd HH:mm");

        private static readonly (string Bg, string Fg)[] AvatarPalette =
        {
            ("#EEEDFE", "#3C3489"), ("#E3F5EE", "#0B5B41"), ("#FBE9F0", "#8A2A55"),
            ("#FBEEDC", "#8A5417"), ("#E8F1FB", "#1F5C96"), ("#F3EFE6", "#5C5646"),
        };

        /// <summary>回傳可以直接放進 style="" 屬性的 CSS 字串，用來畫大頭貼底色。</summary>
        public static string AvatarStyle(string? name)
        {
            var key = string.IsNullOrEmpty(name) ? "?" : name;
            var idx = key.Sum(c => c) % AvatarPalette.Length;
            var (bg, fg) = AvatarPalette[idx];
            return $"background:{bg};color:{fg}";
        }

        /// <summary>會員顯示名稱：優先用暱稱，沒有暱稱就用帳號。</summary>
        public static string DisplayName(string? nickName, string userName)
            => string.IsNullOrWhiteSpace(nickName) ? userName : nickName;
    }
}
