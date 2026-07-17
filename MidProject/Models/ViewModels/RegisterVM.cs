using System.ComponentModel.DataAnnotations;

namespace MidProject.Models.ViewModels
{
    public class RegisterVM
    {
        [Display(Name = "帳號")]
        [Required(ErrorMessage = "{0}必填")]
        [StringLength(50, ErrorMessage = "{0}長度不可大於{1}")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "暱稱")]
        [StringLength(50, ErrorMessage = "{0}長度不可大於{1}")]
        public string? NickName { get; set; }

        [Display(Name = "電子郵件")]
        [Required(ErrorMessage = "{0}必填")]
        [EmailAddress(ErrorMessage = "{0}格式不正確")]
        [StringLength(100, ErrorMessage = "{0}長度不可大於{1}")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "密碼")]
        [Required(ErrorMessage = "{0}必填")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "{0}長度需介於{2}~{1}")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "確認密碼")]
        [Required(ErrorMessage = "{0}必填")]
        [Compare(nameof(Password), ErrorMessage = "{0}與{1}不一致")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
