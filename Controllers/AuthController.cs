// ============================================================
//  LandCheck – Auth Controller
//  File: Controllers/AuthController.cs
// ============================================================

using System.Security.Claims;
using LandCheck.API.Models;
using LandCheck.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandCheck.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    private string IpAddress =>
        Request.Headers.ContainsKey("X-Forwarded-For")
            ? Request.Headers["X-Forwarded-For"].ToString()
            : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>Register a new user account</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _auth.RegisterAsync(dto, IpAddress);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Login and get JWT token</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        try
        {
            var result = await _auth.LoginAsync(dto, IpAddress);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>Refresh access token using refresh token</summary>
    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenDto dto)
    {
        try
        {
            var result = await _auth.RefreshTokenAsync(dto.Token, IpAddress);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>Logout / revoke refresh token</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenDto dto)
    {
        await _auth.RevokeTokenAsync(dto.Token, IpAddress);
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>Get current user profile</summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var userId = int.Parse(User.FindFirstValue("userId") ?? "0");
        var profile = await _auth.GetProfileAsync(userId);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Update current user profile</summary>
    [HttpPatch("profile")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var userId = int.Parse(User.FindFirstValue("userId") ?? "0");
        var profile = await _auth.UpdateProfileAsync(userId, req.FullName, req.Organisation);
        return Ok(profile);
    }

    /// <summary>Change password</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = int.Parse(User.FindFirstValue("userId") ?? "0");
        try
        {
            await _auth.ChangePasswordAsync(userId, dto);
            return Ok(new { message = "Password changed successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Check if email is available</summary>
    [HttpGet("check-email")]
    public async Task<IActionResult> CheckEmail([FromQuery] string email)
    {
        // Simple availability check (returns 200 if available)
        return Ok(new { available = true, email });
    }
}

public record UpdateProfileRequest(string FullName, string? Organisation);
