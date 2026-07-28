using System.ComponentModel.DataAnnotations;

namespace MidProject.Models.DTOs;

// 給列表/詳情頁顯示用
public class ReportDto
{
    public int ReportID { get; set; }
    public int ReporterMemberID { get; set; }
    public string? ReporterUserName { get; set; }

    public int? RestaurantID { get; set; }

    // 檢舉目標所屬的餐廳名稱：目標若直接是餐廳就是該餐廳本身，
    // 若目標是評論/圖片，則是該評論/圖片所屬的餐廳（方便管理員辨識，不論目標類型一律顯示餐廳名稱）
    public string? RestaurantName { get; set; }

    public int? ReviewID { get; set; }
    public int? ImageID { get; set; }

    // 被檢舉會員：該檢舉目標（餐廳/評論/圖片）背後的建立者/上傳者
    public string? ReportedMemberUserName { get; set; }

    // 被檢舉會員的 MemberID（同上，由檢舉目標的建立者/上傳者回推），供列表/詳情頁連結至會員詳細頁
    public int? ReportedMemberID { get; set; }

    // 收件會員是否已停權/刪除（通知模組不發給已刪除會員）：供通知視窗標示「無法通知」原因
    public bool ReporterMemberDeleted { get; set; }
    public bool ReportedMemberDeleted { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Category { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? HandledAt { get; set; }
    public string? HandledByUserName { get; set; }
    public string? AdminNote { get; set; }

    // 方便前台顯示「檢舉目標類型」（使用者只能針對餐廳/評論/圖片檢舉，不含會員）
    public string TargetType =>
        RestaurantID.HasValue ? "餐廳" :
        ReviewID.HasValue ? "評論" :
        ImageID.HasValue ? "圖片" : "未知";

    // 分類由檢舉人送出時選擇，一律顯示使用者選的分類（不依狀態切換）
    public string CategoryDisplay => Category ?? "未分類";

    // 處理狀態的中文顯示
    public string StatusDisplay => Status switch
    {
        "Pending" => "待處理",
        "Approved" => "檢舉成立",
        "Rejected" => "駁回檢舉",
        _ => Status
    };

    // 由 ReportService 使用共用台灣時間計算，避免部署主機時區影響列表顯示。
    public int ProcessingDays { get; set; }

    // 通知檢舉會員審核結果的預設標題／內容範本，管理員送出前可自行編輯
    public string DefaultNotificationTitle => "【檢舉結果通知】您提交的檢舉已完成審核";

    public string DefaultNotificationContent
    {
        get
        {
            var resultLine = Status == "Approved"
                ? "經管理員查核後，確認該內容違反平台社群規範，本次檢舉已成立，我們將依規定處理相關內容。"
                : "經管理員查核後，目前尚未發現該內容違反平台社群規範，因此本次檢舉不予成立，相關內容將維持顯示。";

            return "親愛的會員您好：\n" +
                   "感謝您協助維護平台內容品質。\n" +
                   $"您於 {CreatedAt:yyyy/MM/dd} 提交的檢舉案件，管理員已完成審核。\n" +
                   $"審核結果：{Status}（{StatusDisplay}）\n" +
                   $"{resultLine}\n" +
                   "若您日後發現確實違反平台規範的內容，仍歡迎透過檢舉功能通知我們，我們將盡快進行審查。\n" +
                   "感謝您的理解與支持。\n" +
                   "美食探店平台 管理團隊";
        }
    }

    // 通知被檢舉會員審核結果的預設標題／內容範本：只有「檢舉成立」才需要通知內容擁有者
    public string DefaultReportedMemberNotificationTitle => "【內容審核通知】您的內容已被檢舉審核";

    public string DefaultReportedMemberNotificationContent =>
        "親愛的會員您好：\n" +
        $"您於平台發布的{TargetType}內容於 {CreatedAt:yyyy/MM/dd} 遭到檢舉，管理員已完成審核。\n" +
        "經審核後，確認該內容違反平台社群規範，本次檢舉已成立，我們將依規定處理相關內容，請留意後續處理結果。\n" +
        "若對審核結果有疑問，歡迎透過客服管道與我們聯繫。\n" +
        "感謝您的理解與配合。\n" +
        "美食探店平台 管理團隊";
}

// 檢舉詳情頁「通知紀錄」視窗顯示通知用
public class ReportNotificationRecordDto
{
    public int NotificationID { get; set; }
    public int? MemberID { get; set; }        // 收件會員：用來區分「通知檢舉者」與「通知被檢舉會員」的紀錄
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Outcome { get; set; }      // 處理結果（Approved / Rejected）
    public bool IsSent { get; set; }          // 是否已由通知模組發送
    public DateTime CreatedAt { get; set; }   // 交付通知模組的時間（發送通知模組時間）
    public DateTime ScheduledAt { get; set; } // 排程發送時間
    public DateTime? SentAt { get; set; }     // 實際發送時間（未發送為 null）
}

// 後台處理檢舉的結果（供並行控制／驗證回報）
public enum ReportHandleOutcome
{
    Handled,          // 本次成功定案
    NotFound,
    AlreadyHandled,   // 已被（其他管理員）處理，並行控制擋下
    InvalidStatus,    // 只接受 Approved / Rejected
    InvalidCategory,  // 只接受既定違規分類
    SelfReportNotAllowed, // 不允許會員檢舉自己的內容
    AdminNoteRequired,// 管理員備註必填
    AdminNoteTooLong  // 管理員備註超過長度
}

// 通知檢舉者／被檢舉會員的結果：每種失敗都帶明確訊息給管理員
public sealed class ReportNotifyResult
{
    public bool Success { get; init; }
    public bool AlreadyNotified { get; init; }
    public bool RecipientUnavailable { get; init; }
    public string? Message { get; init; }

    public static ReportNotifyResult Ok() => new() { Success = true };
    public static ReportNotifyResult Already() => new() { AlreadyNotified = true, Message = "此對象已通知過（每個對象只能通知一次）。" };
    public static ReportNotifyResult Recipient(string message) => new() { RecipientUnavailable = true, Message = message };
    public static ReportNotifyResult Fail(string message) => new() { Message = message };
}

// 管理員通知檢舉會員審核結果時使用
public class NotifyReporterDto
{
    [Required(ErrorMessage = "請輸入通知標題")]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入通知內容")]
    public string Content { get; set; } = string.Empty;

    // 待處理案件按「儲存」時一併帶入處理決定：先把檢舉定案（已處理）再建立通知。
    // 已處理案件（通知紀錄重開）則不帶這些，維持原處理狀態不變。
    public string? HandleStatus { get; set; }
    public string? HandleCategory { get; set; }
    public string? HandleAdminNote { get; set; }
}

// 會員送出檢舉時使用：三個目標欄位只能填一個（使用者只能針對餐廳/評論/圖片檢舉，不能檢舉會員）
public class ReportCreateDto
{
    public int? RestaurantID { get; set; }
    public int? ReviewID { get; set; }
    public int? ImageID { get; set; }

    [Required(ErrorMessage = "請選擇檢舉分類")]
    [RegularExpression("不實資訊|廣告洗版|人身攻擊|仇恨言論|色情內容|垃圾訊息", ErrorMessage = "分類值不正確")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "請填寫檢舉原因")]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

// 管理員處理檢舉時使用：分類預設由檢舉人送出時選定，管理員審核時可以修改
public class ReportHandleDto
{
    // 後台處理只接受「檢舉成立(Approved)」或「駁回檢舉(Rejected)」，不接受 Pending
    [Required]
    [RegularExpression("Approved|Rejected", ErrorMessage = "處理結果僅能為檢舉成立或駁回檢舉")]
    public string Status { get; set; } = string.Empty;

    // 違規分類：管理員可覆寫為 6 種違規分類之一；「未分類」為與實體預設一致的合法保留值（不主動提供於下拉選單）
    [RegularExpression("不實資訊|廣告洗版|人身攻擊|仇恨言論|色情內容|垃圾訊息|未分類", ErrorMessage = "分類值不正確")]
    public string? Category { get; set; }

    // 處理檢舉時管理員備註為必填，空白視為未填，最多 30 字
    [Required(ErrorMessage = "處理檢舉時「管理員備註」為必填")]
    [StringLength(30, ErrorMessage = "「管理員備註」最多 30 字")]
    public string? AdminNote { get; set; }
}

// 列表查詢/篩選參數
public class ReportQueryParams
{
    public int? RestaurantID { get; set; }       // 精確篩選指定餐廳的檢舉
    public int? ReviewID { get; set; }           // 精確篩選指定評論的檢舉
    public string? Keyword { get; set; }         // 搜尋原因/檢舉者/被檢舉會員
    public string? Status { get; set; }          // Pending / Approved / Rejected
    public string? TargetType { get; set; }      // Restaurant / Review / Image（不含 Member）
    public string? Category { get; set; }  // 不實資訊 / 廣告洗版 / 人身攻擊 / 仇恨言論 / 色情內容 / 垃圾訊息
    public DateTime? DateFrom { get; set; }       // 檢舉日期（從）
    public DateTime? DateTo { get; set; }         // 檢舉日期（至）
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "CreatedAt";      // ReportID / Status / CreatedAt / ProcessingDays
    public string SortDirection { get; set; } = "desc";    // asc / desc
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// Dashboard 總覽頁使用
public class ReportDashboardDto
{
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int PendingNewThisMonth { get; set; }
    public List<MonthlyReportStatDto> MonthlyStats { get; set; } = new();
}

public class MonthlyReportStatDto
{
    public string MonthLabel { get; set; } = string.Empty; // 例如 "2026/07"
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Pending { get; set; }
}

// Repository 內部用來撈取「狀態＋建立時間」的輕量投影，不用整包 Report entity
public class ReportStatusDatePoint
{
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
