using System.ComponentModel.DataAnnotations;

namespace MidProject.Models.ViewModels
{
    public class MemberEditVM
    {
        public int MemberID { get; set; }

        [Required, StringLength(20)]
        [RegularExpression("^(Normal|Warning|Muted|Suspended)$", ErrorMessage = "會員狀態不正確。")]
        public string Status { get; set; } = "Normal";

        public string? AdminNote { get; set; }

        public string? StatusChangeReason { get; set; }

        public string? NickName { get; set; }
        public string? NicknameChangeReason { get; set; }

        public bool RemoveAvatarRequested { get; set; }
        public string? AvatarRemovalReason { get; set; }

        [Range(0, int.MaxValue)]
        public int Points { get; set; }
        public string? PointsChangeReason { get; set; }

        public bool UnlockAccountRequested { get; set; }
        public string? UnlockReason { get; set; }
    }
}
