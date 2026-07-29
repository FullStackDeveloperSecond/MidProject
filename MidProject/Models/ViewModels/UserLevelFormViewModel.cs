using System.ComponentModel.DataAnnotations;

namespace MidProject.Models.ViewModels;

public sealed class UserLevelFormViewModel
{
    public int LevelID { get; set; }

    [Required(ErrorMessage = "請輸入等級名稱。")]
    [StringLength(50, ErrorMessage = "等級名稱最多 50 個字。")]
    public string LevelName { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "最低經驗值不可小於 0。")]
    public int MinExp { get; set; }

    [StringLength(50, ErrorMessage = "獎勵說明最多 50 個字。")]
    public string? Rewards { get; set; }
}
