// ============================================================
//  LandCheck – Auth Models & DTOs
//  File: Models/AuthModels.cs
// ============================================================

namespace LandCheck.API.Models;

// ── User Entity ────────────────────────────────────────────
public class AppUser
{
    public int       Id           { get; set; }
    public string    FullName     { get; set; } = string.Empty;
    public string    Email        { get; set; } = string.Empty;
    public string    Phone        { get; set; } = string.Empty;
    public string    PasswordHash { get; set; } = string.Empty;
    public UserRole  Role         { get; set; } = UserRole.Farmer;
    public string?   Organisation { get; set; }          // Bank name / Firm name
    public string?   AadhaarRef   { get; set; }          // masked last 4 digits only
    public bool      IsActive     { get; set; } = true;
    public bool      IsVerified   { get; set; } = false;
    public DateTime  CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime  LastLoginAt  { get; set; } = DateTime.UtcNow;

    public List<RefreshToken> RefreshTokens { get; set; } = new();
}

// ── Refresh Token ──────────────────────────────────────────
public class RefreshToken
{
    public int      Id         { get; set; }
    public int      AppUserId  { get; set; }
    public string   Token      { get; set; } = string.Empty;
    public DateTime ExpiresAt  { get; set; }
    public bool     IsRevoked  { get; set; }
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public string?  CreatedByIp{ get; set; }

    public AppUser AppUser { get; set; } = null!;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive  => !IsRevoked && !IsExpired;
}

// ── Request DTOs ───────────────────────────────────────────
public record RegisterDto(
    string FullName,
    string Email,
    string Phone,
    string Password,
    UserRole Role,
    string? Organisation
);

public record LoginDto(
    string Email,
    string Password
);

public record RefreshTokenDto(string Token);

public record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword
);

// ── Response DTOs ──────────────────────────────────────────
public class AuthResponseDto
{
    public string   AccessToken    { get; set; } = string.Empty;
    public string   RefreshToken   { get; set; } = string.Empty;
    public DateTime ExpiresAt      { get; set; }
    public UserProfileDto User     { get; set; } = null!;
}

public class UserProfileDto
{
    public int      Id           { get; set; }
    public string   FullName     { get; set; } = string.Empty;
    public string   Email        { get; set; } = string.Empty;
    public string   Phone        { get; set; } = string.Empty;
    public string   Role         { get; set; } = string.Empty;
    public string   RoleIcon     { get; set; } = string.Empty;
    public string?  Organisation { get; set; }
    public bool     IsVerified   { get; set; }
    public DateTime CreatedAt    { get; set; }
    public DateTime LastLoginAt  { get; set; }
    public List<string> Permissions { get; set; } = new();
}
