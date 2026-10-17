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
    IJwtTokenService jwtTokenService) : ControllerBase
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
}
