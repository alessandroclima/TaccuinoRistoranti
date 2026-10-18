using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaccuinoRistoranti.Api.Contracts;
using TaccuinoRistoranti.Api.Data;
using TaccuinoRistoranti.Api.Models;
using TaccuinoRistoranti.Api.Services;

namespace TaccuinoRistoranti.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtTokenService jwtTokenService,
    IEmailService emailService,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return BadRequest("Email gia registrata.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(e => e.Description));
        }

        var auth = await GenerateAuthResponseAsync(user);
        return Ok(auth);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized("Credenziali non valide.");
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
        {
            return Unauthorized("Credenziali non valide.");
        }

        var auth = await GenerateAuthResponseAsync(user);
        return Ok(auth);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request)
    {
        var tokenValue = request.RefreshToken.Trim();
        var tokenHash = HashRefreshToken(tokenValue);
        var storedToken = await dbContext.RefreshTokens
            .FindAsync([GetRefreshTokenId(tokenValue)]);

        if (storedToken is null || storedToken.Token != tokenHash)
        {
            return Unauthorized("Refresh token non valido o scaduto.");
        }

        if (!storedToken.IsActive)
        {
            await RevokeAllActiveUserTokensAsync(storedToken.UserId, "Riuso refresh token revocato");
            return Unauthorized("Sessione invalidata per possibile compromissione token.");
        }

        var user = await userManager.FindByIdAsync(storedToken.UserId);
        if (user is null)
        {
            return Unauthorized("Utente non trovato.");
        }

        storedToken.RevokedAtUtc = DateTime.UtcNow;
        storedToken.RevokedReason = "Ruotato al refresh";

        var refreshTokenRaw = GenerateRefreshTokenRaw();
        var nextTokenId = GetRefreshTokenId(refreshTokenRaw);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(jwtTokenService.Options.RefreshTokenExpirationDays);

        storedToken.ReplacedByTokenId = nextTokenId;

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = nextTokenId,
            Token = HashRefreshToken(refreshTokenRaw),
            UserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = refreshExpiresAt
        });

        await dbContext.SaveChangesAsync();

        var accessToken = jwtTokenService.GenerateToken(user);
        var accessExpiresAt = jwtTokenService.GetAccessTokenExpirationUtc();
        var auth = new AuthResponse(
            accessToken,
            refreshTokenRaw,
            user.Email ?? string.Empty,
            accessExpiresAt,
            refreshExpiresAt);

        return Ok(auth);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request)
    {
        var tokenValue = request.RefreshToken.Trim();
        var tokenHash = HashRefreshToken(tokenValue);
        var tokenId = GetRefreshTokenId(tokenValue);

        var refreshToken = await dbContext.RefreshTokens.FindAsync([tokenId]);
        if (refreshToken is null || refreshToken.Token != tokenHash)
        {
            return Ok();
        }

        refreshToken.RevokedAtUtc = DateTime.UtcNow;
        refreshToken.RevokedReason = "Logout utente";
        await dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword(PasswordResetRequest request)
    {
        try
        {
            var ipAddress = GetClientIpAddress();
            await RequestPasswordResetAsync(request, ipAddress);

            // Always return success to prevent email enumeration attacks.
            return Ok(new { message = "If the email address is registered, you will receive a password reset link shortly." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in forgot-password for email {Email}", request.Email);
            return StatusCode(500, new { message = "An error occurred while processing your request." });
        }
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword(ResetPasswordRequest request)
    {
        try
        {
            var ipAddress = GetClientIpAddress();
            await ResetPasswordAsync(request, ipAddress);

            return Ok(new { message = "Password has been reset successfully. You can now log in with your new password." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in reset-password");
            return StatusCode(500, new { message = "An error occurred while resetting your password." });
        }
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user)
    {
        var refreshTokenRaw = GenerateRefreshTokenRaw();
        var now = DateTime.UtcNow;
        var refreshExpiresAt = now.AddDays(jwtTokenService.Options.RefreshTokenExpirationDays);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = GetRefreshTokenId(refreshTokenRaw),
            Token = HashRefreshToken(refreshTokenRaw),
            UserId = user.Id,
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpiresAt
        });

        await dbContext.SaveChangesAsync();

        var accessToken = jwtTokenService.GenerateToken(user);
        var accessExpiresAt = jwtTokenService.GetAccessTokenExpirationUtc();

        return new AuthResponse(
            accessToken,
            refreshTokenRaw,
            user.Email ?? string.Empty,
            accessExpiresAt,
            refreshExpiresAt);
    }

    private async Task RequestPasswordResetAsync(PasswordResetRequest request, string ipAddress)
    {
        var normalizedEmail = (request.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return;
        }

        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail.ToUpper());

        if (user is null)
        {
            // Don't reveal if email exists or not for security.
            return;
        }

        var existingTokens = await dbContext.PasswordResetTokens
            .Where(prt => prt.UserId == user.Id && !prt.IsUsed && prt.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in existingTokens)
        {
            token.IsUsed = true;
            token.UsedAtUtc = DateTime.UtcNow;
        }

        var resetToken = GenerateSecureToken();
        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            Token = resetToken,
            UserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            IpAddress = ipAddress
        });

        await dbContext.SaveChangesAsync();

        try
        {
            await emailService.SendPasswordResetEmailAsync(user.Email ?? normalizedEmail, user.UserName ?? user.Email ?? "Utente", resetToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Password reset token created for user {UserId} but email delivery failed", user.Id);
        }
    }

    private async Task ResetPasswordAsync(ResetPasswordRequest request, string ipAddress)
    {
        var normalizedToken = (request.Token ?? string.Empty).Trim().Replace(' ', '+');
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            throw new InvalidOperationException("Invalid reset token");
        }

        var passwordResetToken = await dbContext.PasswordResetTokens
            .Include(prt => prt.User)
            .FirstOrDefaultAsync(prt => prt.Token == normalizedToken);

        if (passwordResetToken is null)
        {
            throw new InvalidOperationException("Invalid reset token");
        }

        if (passwordResetToken.IsUsed)
        {
            throw new InvalidOperationException("Reset token has already been used");
        }

        if (passwordResetToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Reset token has expired");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            throw new InvalidOperationException("Password must be at least 6 characters long");
        }

        var user = passwordResetToken.User;
        if (user is null)
        {
            throw new InvalidOperationException("Invalid reset token");
        }

        var identityResetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, identityResetToken, request.NewPassword);
        if (!result.Succeeded)
        {
            var errorMessage = result.Errors.FirstOrDefault()?.Description
                               ?? "Could not reset password";
            throw new InvalidOperationException(errorMessage);
        }

        passwordResetToken.IsUsed = true;
        passwordResetToken.UsedAtUtc = DateTime.UtcNow;
        passwordResetToken.IpAddress = ipAddress;

        var userRefreshTokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAtUtc == null && DateTime.UtcNow < rt.ExpiresAtUtc)
            .ToListAsync();

        foreach (var refreshToken in userRefreshTokens)
        {
            refreshToken.RevokedAtUtc = DateTime.UtcNow;
            refreshToken.RevokedReason = "Password reset";
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task RevokeAllActiveUserTokensAsync(string userId, string reason)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            token.RevokedReason = reason;
        }

        if (activeTokens.Count > 0)
        {
            await dbContext.SaveChangesAsync();
        }
    }

    private static string GenerateRefreshTokenRaw()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static Guid GetRefreshTokenId(string refreshTokenRaw)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshTokenRaw));
        return new Guid(hash[..16]);
    }

    private static string HashRefreshToken(string refreshTokenRaw)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshTokenRaw));
        return Convert.ToHexString(hash);
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private string GetClientIpAddress()
    {
        var ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();

        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = Request.Headers["X-Real-IP"].FirstOrDefault();
        }

        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        return ipAddress ?? "Unknown";
    }
}
