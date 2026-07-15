using Microsoft.AspNetCore.Mvc;
using MidProject.Services.IServices;

namespace MidProject.Controllers
{
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
            int page = 1)
        {
            var vm = await _reviewService.GetReviewListAsync(tab, search, rating, time, page);
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
            return 1; 
        }
    }
}
