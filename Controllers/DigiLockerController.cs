// ============================================================
//  LandCheck — DigiLocker Controller
//  File: Controllers/DigiLockerController.cs
// ============================================================

using LandCheck.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandCheck.API.Controllers;

[ApiController]
[Route("api/digilocker")]
public class DigiLockerController : ControllerBase
{
    private readonly IDigiLockerService _digiLocker;

    public DigiLockerController(IDigiLockerService digiLocker)
        => _digiLocker = digiLocker;

    /// <summary>Generate DigiLocker verification link</summary>
    [HttpPost("generate-link")]
    public async Task<IActionResult> GenerateLink([FromBody] GenerateLinkRequest req)
    {
        try
        {
            var redirectUrl = req.RedirectUrl ?? "https://landcheck-frontend.vercel.app/digilocker-callback";
            var result = await _digiLocker.GenerateLinkAsync(redirectUrl, req.LandRecordId);

            if (result.Success)
                return Ok(new {
                    success = true,
                    link = result.Link,
                    requestId = result.RequestId,
                    message = result.Message
                });

            return BadRequest(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>Get documents from DigiLocker after user consent</summary>
    [HttpGet("get-documents/{requestId}")]
    public async Task<IActionResult> GetDocuments(string requestId)
    {
        try
        {
            var result = await _digiLocker.GetDocumentsAsync(requestId);
            return Ok(new {
                success = result.Success,
                data = result.RawData,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>Test Surepass connection</summary>
    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new {
            success = true,
            message = "DigiLocker service is ready!",
            info = "Use POST /api/digilocker/generate-link to start verification"
        });
    }
}

public record GenerateLinkRequest(
    int LandRecordId,
    string? RedirectUrl
);
