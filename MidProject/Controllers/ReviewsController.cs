using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidProject.Services.IServices;
using System.Security.Claims;

namespace MidProject.Controllers
{
    /// <summary>
    /// 評論管理後台。對應規格書「Terry 評論模組」的 9 條完成標準。
    ///
    /// 這層只做「接請求 → 呼叫 Service → 決定回什麼」，實際查資料庫的邏輯在
    /// Repositories/ReviewRepository.cs，商業邏輯（統計、重算、組 ViewModel）在
    /// Services/ReviewService.cs。
    ///
    /// 假設條件（跟愷核對，如果名稱不同要照他實際的改）：
    /// 1. Review 有 navigation property：Member、Restaurant、ReviewImages（ReviewImages 裡有 Image）、DeletedByMember（透過 DeletedBy 關聯 Members）
    /// 2. Report 有 navigation property：ReporterMember（透過 ReporterMemberID 關聯 Members）
    /// 3. GetCurrentAdminMemberId() 已改讀登入 Cookie 的 ClaimTypes.NameIdentifier（AccountController.Login 簽發）
    /// 4. IReviewRepository / IReviewService 要在 Program.cs 註冊 DI（見這個檔案最下面的說明，
    ///    Program.cs 不是我能自己改的檔案，要請愷/Alex 加兩行）
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class ReviewsController : Controller
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        // GET: /Reviews
        public async Task<IActionResult> Index(
            string tab = "all",
            string? search = null,
            int? rating = null,
            string time = "all",
            string sortBy = "time",
            string sortDir = "desc",
            int page = 1)
        {
            var vm = await _reviewService.GetReviewListAsync(tab, search, rating, time, sortBy, sortDir, page);
            return View(vm);
        }

        // GET: /Reviews/Details/127
        public async Task<IActionResult> Details(int id)
        {
            var vm = await _reviewService.GetReviewDetailAsync(id);
            if (vm == null)
            {
                return NotFound();
            }
            return View(vm);
        }

        // POST: /Reviews/SoftDelete/127
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var ok = await _reviewService.SoftDeleteAsync(id, GetCurrentAdminMemberId());
            if (!ok)
            {
                return NotFound();
            }

            TempData["Toast"] = $"評論 #{id} 已軟刪除";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Reviews/Restore/127
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var ok = await _reviewService.RestoreAsync(id);
            if (!ok)
            {
                return NotFound();
            }

            TempData["Toast"] = $"評論 #{id} 已恢復顯示";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Reviews/DeleteImage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int imageId, int reviewId)
        {
            var ok = await _reviewService.DeleteImageAsync(imageId, GetCurrentAdminMemberId());
            if (!ok)
            {
                return NotFound();
            }

            TempData["Toast"] = "圖片已刪除（軟刪除，實體檔案保留）";
            return RedirectToAction(nameof(Details), new { id = reviewId });
        }

        private int GetCurrentAdminMemberId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
