using System.ComponentModel.DataAnnotations;

namespace MidProject.Models.ViewModels
{
    public class MemberEditVM
    {
        public int MemberID { get; set; }

        public string RowVersion { get; set; } = string.Empty;

        [Required, StringLength(20)]
        [RegularExpression("^(Normal|Warning|Muted|Suspended)$", ErrorMessage = "會員狀態不正確。")]
        public string Status { get; set; } = "Normal";

        public string? AdminNote { get; set; }

        [StringLength(200)]
        public string? StatusChangeReason { get; set; }

        [StringLength(50)]
        public string? NickName { get; set; }
        [StringLength(200)]
        public string? NicknameChangeReason { get; set; }

        public bool RemoveAvatarRequested { get; set; }
        [StringLength(200)]
        public string? AvatarRemovalReason { get; set; }

        [Range(0, int.MaxValue)]
        public int Points { get; set; }
        [StringLength(200)]
        public string? PointsChangeReason { get; set; }

        public bool UnlockAccountRequested { get; set; }
        [StringLength(200)]
        public string? UnlockReason { get; set; }
    }
}
