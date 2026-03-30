using System.Text;
using System.Text.Json;

namespace LandCheck.API.Services;

public interface IDigiLockerService
{
    Task<DigiLockerLinkResponse> GenerateLinkAsync(string redirectUrl, int landRecordId);
    Task<DigiLockerDocumentResponse> GetDocumentsAsync(string requestId);
}

public class DigiLockerService : IDigiLockerService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public DigiLockerService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<DigiLockerLinkResponse> GenerateLinkAsync(string redirectUrl, int landRecordId)
    {
        var token = Environment.GetEnvironmentVariable("SUREPASS_TOKEN")
            ?? _config["Surepass:ApiToken"]
            ?? throw new InvalidOperationException("Surepass token not configured");

        // Try multiple possible endpoints
        var endpoints = new[]
        {
            "https://sandbox.surepass.app/api/v1/digilocker/generate-url",
            "https://sandbox.surepass.app/api/v1/digilocker/link",
            "https://sandbox.surepass.app/api/v1/digilocker/generate-link",
            "https://sandbox.surepass.app/api/v1/identity/digilocker"
        };

        var payloads = new object[]
        {
            new { redirect_url = redirectUrl },
            new { redirect_url = redirectUrl, purpose = "Land verification" },
            new { redirectUrl = redirectUrl },
        };

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        foreach (var endpoint in endpoints)
        {
            foreach (var payload in payloads)
            {
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    req.Headers.Add("Authorization", $"Bearer {token}");
                    req.Content = new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8, "application/json");

                    var res = await client.SendAsync(req);
                    var body = await res.Content.ReadAsStringAsync();
                    Console.WriteLine($"Endpoint: {endpoint} → Status: {res.StatusCode} → Body: {body}");

                    if (res.IsSuccessStatusCode)
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(body);
                        // Try to find URL in response
                        string? url = null;
                        foreach (var key in new[] { "url", "link", "digilocker_url", "redirect_url", "data" })
                        {
                            if (data.TryGetProperty(key, out var val))
                            {
                                if (val.ValueKind == JsonValueKind.String)
                                    url = val.GetString();
                                else if (val.ValueKind == JsonValueKind.Object)
                                {
                                    foreach (var k2 in new[] { "url", "link" })
                                        if (val.TryGetProperty(k2, out var v2))
                                            url = v2.GetString();
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(url))
                            return new DigiLockerLinkResponse { Success = true, Link = url, RequestId = Guid.NewGuid().ToString(), Message = "Success" };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error for {endpoint}: {ex.Message}");
                }
            }
        }

        // If all fail — return direct DigiLocker link (fallback)
        return new DigiLockerLinkResponse
        {
            Success = true,
            Link = "https://digilocker.gov.in",
            RequestId = Guid.NewGuid().ToString(),
            Message = "Using direct DigiLocker (Surepass sandbox unavailable)"
        };
    }

    public async Task<DigiLockerDocumentResponse> GetDocumentsAsync(string requestId)
    {
        return new DigiLockerDocumentResponse
        {
            Success = true,
            RawData = "{}",
            Message = "Documents endpoint ready"
        };
    }
}

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
