namespace TaccuinoRistoranti.Api.Contracts;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record PasswordResetRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);

public record AuthResponse(
	string Token,
	string RefreshToken,
	string Email,
	DateTime AccessTokenExpiresAtUtc,
	DateTime RefreshTokenExpiresAtUtc);
