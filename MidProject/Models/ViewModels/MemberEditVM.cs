using System.ComponentModel.DataAnnotations;

namespace MidProject.Models.ViewModels
{
    public class MemberEditVM
    {
        public int MemberID { get; set; }

        [Required, StringLength(20)]
        public string Status { get; set; } = "Normal";

        public string? AdminNote { get; set; }

        public string? StatusChangeReason { get; set; }
    }
}
