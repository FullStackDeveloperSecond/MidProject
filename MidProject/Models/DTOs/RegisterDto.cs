using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EStoreFrontEnd.Models.DTOs
{
	public class RegisterDto
	{
		public string Account { get; set; }

		/// <summary>
		/// 明文密碼，將在後端進行雜湊處理
		/// </summary>
		public string Password { get; set; }

		/// <summary>
		/// 雜湊後的密碼，將在後端進行雜湊處理
		/// </summary>
		public string HashedPassword { get; set; }

		public string Email { get; set; }

		public string Name { get; set; }

		public string Mobile { get; set; }

		public bool? IsConfirmed { get; set; }

		public string NewMemberConfirmCode { get; set; }
	}
}
