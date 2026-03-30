// ============================================================
//  LandCheck — Surepass DigiLocker Service
//  File: Services/DigiLockerService.cs
//  Purpose: Generate DigiLocker link for land document verification
// ============================================================

using System.Text;
using System.Text.Json;
using LandCheck.API.Models;

namespace LandCheck.API.Services;

public interface IDigiLockerService
{
    Task<DigiLockerLinkResponse> GenerateLinkAsync(string redirectUrl, int landRecordId);
    Task<DigiLockerDocumentResponse> GetDocumentsAsync(string requestId);
}

public class DigiLockerService : IDigiLockerService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    public DigiLockerService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _http = httpClientFactory.CreateClient("Surepass");
    }

    // ── Generate DigiLocker Link ──────────────────────────
    public async Task<DigiLockerLinkResponse> GenerateLinkAsync(string redirectUrl, int landRecordId)
    {
        var token = _config["Surepass:ApiToken"]
            ?? Environment.GetEnvironmentVariable("SUREPASS_TOKEN")
            ?? throw new InvalidOperationException("Surepass token not configured");

        var payload = new
        {
            redirect_url = redirectUrl,
            purpose = $"Land document verification for LandCheck record #{landRecordId}"
        };

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/digilocker/generate-url"
        );

        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        try
        {
            var response = await _http.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Surepass DigiLocker response: {content}");

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<SurepassResponse>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return new DigiLockerLinkResponse
                {
                    Success = true,
                    Link = result?.Data?.Url ?? result?.Data?.Link ?? "",
                    RequestId = result?.Data?.ClientId ?? Guid.NewGuid().ToString(),
                    Message = "DigiLocker link generated successfully"
                };
            }
            else
            {
                return new DigiLockerLinkResponse
                {
                    Success = false,
                    Message = $"Surepass API error: {content}"
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DigiLocker error: {ex.Message}");
            return new DigiLockerLinkResponse
            {
                Success = false,
                Message = $"Service error: {ex.Message}"
            };
        }
    }

    // ── Get Documents from DigiLocker ─────────────────────
    public async Task<DigiLockerDocumentResponse> GetDocumentsAsync(string requestId)
    {
        var token = _config["Surepass:ApiToken"]
            ?? Environment.GetEnvironmentVariable("SUREPASS_TOKEN")
            ?? throw new InvalidOperationException("Surepass token not configured");

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/digilocker/get-document?client_id={requestId}"
        );

        request.Headers.Add("Authorization", $"Bearer {token}");

        try
        {
            var response = await _http.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"DigiLocker documents response: {content}");

            return new DigiLockerDocumentResponse
            {
                Success = response.IsSuccessStatusCode,
                RawData = content,
                Message = response.IsSuccessStatusCode
                    ? "Documents retrieved successfully"
                    : $"Error: {content}"
            };
        }
        catch (Exception ex)
        {
            return new DigiLockerDocumentResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}

// ── Response Models ────────────────────────────────────────
public class DigiLockerLinkResponse
{
    public bool Success { get; set; }
    public string Link { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class DigiLockerDocumentResponse
{
    public bool Success { get; set; }
    public string RawData { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class SurepassResponse
{
    public bool Status_code { get; set; }
    public SurepassData? Data { get; set; }
    public string? Message { get; set; }
}

public class SurepassData
{
    public string? Url { get; set; }
    public string? Link { get; set; }
    public string? ClientId { get; set; }
    public string? Client_id { get; set; }
}
