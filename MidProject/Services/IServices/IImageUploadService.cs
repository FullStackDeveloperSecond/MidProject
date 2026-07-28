using Microsoft.AspNetCore.Http;
using MidProject.Models;

namespace MidProject.Services.IServices;

public interface IImageUploadService
{
    Task<Image> SaveAsync(IFormFile file, string imageType, int uploadedByMemberId);

    /// <summary>刪除實體檔案（不動資料庫）。給 Service 層在 DB 交易失敗、需要清理已寫入硬碟的孤立檔案時呼叫。</summary>
    Task DeleteAsync(string imageUrl);
}
