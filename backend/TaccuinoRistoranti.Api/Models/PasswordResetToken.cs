namespace TaccuinoRistoranti.Api.Models;

public class PasswordResetToken
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(1);
    public bool IsUsed { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public string? IpAddress { get; set; }

    public ApplicationUser? User { get; set; }
}
