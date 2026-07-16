using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;

namespace EStoreFrontEnd.Models.ViewModels
{
	public class LoginVM
	{
		[Display(Name="帳號")]
		[Required(ErrorMessage = "{0}必填")]
		public string Account { get; set; }

		[Display(Name = "密碼")]
		[Required(ErrorMessage = "{0}必填")]
		[DataType(DataType.Password)]
		public string Password { get; set; }
	}
}

