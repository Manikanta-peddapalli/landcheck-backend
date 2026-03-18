// ============================================================
//  LandCheck – Authentication Service
//  File: Services/AuthService.cs
// ============================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LandCheck.API.Data;
using LandCheck.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LandCheck.API.Services;

public interface IAuthService
{
    Task<AuthResponseDto>   RegisterAsync(RegisterDto dto, string ipAddress);
    Task<AuthResponseDto>   LoginAsync(LoginDto dto, string ipAddress);
    Task<AuthResponseDto>   RefreshTokenAsync(string token, string ipAddress);
    Task                    RevokeTokenAsync(string token, string ipAddress);
    Task<UserProfileDto?>   GetProfileAsync(int userId);
    Task<UserProfileDto>    UpdateProfileAsync(int userId, string fullName, string? organisation);
    Task<bool>              ChangePasswordAsync(int userId, ChangePasswordDto dto);
}

public class AuthService : IAuthService
{
    private readonly LandCheckDbContext _db;
    private readonly IConfiguration    _config;

    public AuthService(LandCheckDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    // ── REGISTER ────────────────────────────────────────────
    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto, string ipAddress)
    {
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
            throw new InvalidOperationException("An account with this email already exists.");

        var user = new AppUser
        {
            FullName     = dto.FullName.Trim(),
            Email        = dto.Email.ToLower().Trim(),
            Phone        = dto.Phone.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role         = dto.Role,
            Organisation = dto.Organisation,
            IsActive     = true,
            IsVerified   = false,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return await GenerateAuthResponseAsync(user, ipAddress);
    }

    // ── LOGIN ────────────────────────────────────────────────
    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, string ipAddress)
    {
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());

        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is deactivated. Contact support.");

        // Remove expired refresh tokens
        user.RefreshTokens.RemoveAll(t => !t.IsActive);
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GenerateAuthResponseAsync(user, ipAddress);
    }

    // ── REFRESH TOKEN ────────────────────────────────────────
    public async Task<AuthResponseDto> RefreshTokenAsync(string token, string ipAddress)
    {
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == token));

        if (user is null) throw new UnauthorizedAccessException("Invalid refresh token.");

        var refreshToken = user.RefreshTokens.Single(t => t.Token == token);

        if (!refreshToken.IsActive)
            throw new UnauthorizedAccessException("Refresh token is expired or revoked.");

        // Rotate: revoke old, issue new
        refreshToken.IsRevoked = true;

        return await GenerateAuthResponseAsync(user, ipAddress);
    }

    // ── REVOKE TOKEN ─────────────────────────────────────────
    public async Task RevokeTokenAsync(string token, string ipAddress)
    {
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == token));

        if (user is null) return;

        var refreshToken = user.RefreshTokens.FirstOrDefault(t => t.Token == token);
        if (refreshToken is not null) refreshToken.IsRevoked = true;

        await _db.SaveChangesAsync();
    }

    // ── GET PROFILE ──────────────────────────────────────────
    public async Task<UserProfileDto?> GetProfileAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user is null ? null : MapToProfile(user);
    }

    // ── UPDATE PROFILE ───────────────────────────────────────
    public async Task<UserProfileDto> UpdateProfileAsync(int userId, string fullName, string? organisation)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        user.FullName     = fullName.Trim();
        user.Organisation = organisation;
        await _db.SaveChangesAsync();
        return MapToProfile(user);
    }

    // ── CHANGE PASSWORD ──────────────────────────────────────
    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return false;

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── PRIVATE: Generate JWT + Refresh Token ────────────────
    private async Task<AuthResponseDto> GenerateAuthResponseAsync(AppUser user, string ipAddress)
    {
        var accessToken  = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken(ipAddress);

        user.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken  = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt    = DateTime.UtcNow.AddHours(2),
            User         = MapToProfile(user)
        };
    }

    private string GenerateJwtToken(AppUser user)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                          _config["Jwt:Secret"] ?? "LandCheck-Super-Secret-Key-2024-India"));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(2);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name,  user.FullName),
            new Claim(ClaimTypes.Role,               user.Role.ToString()),
            new Claim("role",                        user.Role.ToString()),
            new Claim("userId",                      user.Id.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"] ?? "LandCheck",
            audience:           _config["Jwt:Audience"] ?? "LandCheckUsers",
            claims:             claims,
            expires:            expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static RefreshToken GenerateRefreshToken(string ipAddress) => new()
    {
        Token         = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        ExpiresAt     = DateTime.UtcNow.AddDays(30),
        CreatedByIp   = ipAddress
    };

    // ── Role → Permissions mapping ───────────────────────────
    private static UserProfileDto MapToProfile(AppUser user)
    {
        var (icon, perms) = user.Role switch
        {
            UserRole.Farmer => ("🌾", new List<string> {
                "view:own_records", "create:record", "upload:document", "view:risk_report"
            }),
            UserRole.Bank => ("🏦", new List<string> {
                "view:all_records", "create:record", "upload:document",
                "view:risk_report", "run:risk_analysis", "download:report", "view:dashboard"
            }),
            UserRole.Lawyer => ("⚖️", new List<string> {
                "view:all_records", "view:risk_report", "view:ownership_chain",
                "run:risk_analysis", "download:report", "add:ownership_history"
            }),
            UserRole.RealEstateAgent => ("🏢", new List<string> {
                "view:all_records", "create:record", "upload:document",
                "view:risk_report", "run:risk_analysis"
            }),
            UserRole.NRI => ("✈️", new List<string> {
                "view:own_records", "create:record", "upload:document",
                "view:risk_report", "download:report"
            }),
            UserRole.Government => ("🏛", new List<string> {
                "view:all_records", "create:record", "upload:document",
                "view:risk_report", "run:risk_analysis", "download:report",
                "view:dashboard", "manage:users", "update:status"
            }),
            _ => ("👤", new List<string>())
        };

        return new UserProfileDto
        {
            Id           = user.Id,
            FullName     = user.FullName,
            Email        = user.Email,
            Phone        = user.Phone,
            Role         = user.Role.ToString(),
            RoleIcon     = icon,
            Organisation = user.Organisation,
            IsVerified   = user.IsVerified,
            CreatedAt    = user.CreatedAt,
            LastLoginAt  = user.LastLoginAt,
            Permissions  = perms
        };
    }
}
