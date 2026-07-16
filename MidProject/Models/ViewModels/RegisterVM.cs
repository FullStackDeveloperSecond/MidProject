using System.ComponentModel.DataAnnotations;

namespace EStoreFrontEnd.Models.ViewModels
{
	public class RegisterVM
	{
		[Display(Name ="帳號")]
		[Required(ErrorMessage = "{0}必填")]
		[StringLength(30, ErrorMessage = "{0}長度不可大於{1}")]
		public string Account { get; set; }

		[Display(Name = "密碼")]
		[Required(ErrorMessage = "{0}必填")]
		[StringLength(20, ErrorMessage = "{0}長度不可大於{1}")]
		[DataType(DataType.Password)]
		public string Password { get; set; }

		[Display(Name = "確認密碼")]
		[Required(ErrorMessage = "{0}必填")]
		[Compare("Password", ErrorMessage = "{0}與{1}不一致")]
		[DataType(DataType.Password)]
		public string ConfirmPassword { get; set; }

		[Display(Name = "電子郵件")]
		[Required(ErrorMessage = "{0}必填")]
		[EmailAddress(ErrorMessage = "{0}格式不正確")]
		public string Email { get; set; }

		[Display(Name = "姓名")]
		[Required(ErrorMessage = "{0}必填")]
		[StringLength(30, ErrorMessage = "{0}長度不可大於{1}")]
		public string Name { get; set; }

		[Display(Name = "手機號碼")]
		[Required(ErrorMessage = "{0}必填")]
		[StringLength(10, ErrorMessage = "{0}長度不可大於{1}")]
		public string Mobile { get; set; }
	}
}
