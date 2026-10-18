using Microsoft.AspNetCore.Identity;

namespace TaccuinoRistoranti.Api.Models;

public class ApplicationUser : IdentityUser
{
	public List<PasswordResetToken> PasswordResetTokens { get; set; } = [];
}
