// ============================================================
//  LandCheck – Data Transfer Objects
//  File: Models/DTOs.cs
// ============================================================

namespace LandCheck.API.Models;

// ── Land Record DTOs ───────────────────────────────────────
public record CreateLandRecordDto(
    string SurveyNumber,
    string Village,
    string Mandal,
    string District,
    string State,
    decimal ExtentAcres,
    string LandType,
    string CurrentOwner
);

public record UpdateLandRecordDto(
    string? Village,
    string? Mandal,
    string? District,
    decimal? ExtentAcres,
    string? LandType,
    string? CurrentOwner,
    RecordStatus? Status
);

public class LandRecordResponseDto
{
    public int           Id             { get; set; }
    public string        SurveyNumber   { get; set; } = string.Empty;
    public string        Village        { get; set; } = string.Empty;
    public string        Mandal         { get; set; } = string.Empty;
    public string        District       { get; set; } = string.Empty;
    public string        State          { get; set; } = string.Empty;
    public decimal       ExtentAcres    { get; set; }
    public string        LandType       { get; set; } = string.Empty;
    public string        CurrentOwner   { get; set; } = string.Empty;
    public string        Status         { get; set; } = string.Empty;
    public DateTime      CreatedAt      { get; set; }
    public RiskSummaryDto? RiskSummary  { get; set; }
    public List<OwnershipHistoryDto> OwnershipChain { get; set; } = new();
    public int           DocumentCount  { get; set; }
}

// ── Ownership History DTOs ────────────────────────────────
public record CreateOwnershipHistoryDto(
    int LandRecordId,
    string OwnerName,
    DateTime TransferDate,
    DateTime? EndDate,
    string DocumentType,
    string DocumentRef,
    TransferType TransferType,
    bool IsVerified,
    string? Notes
);

public class OwnershipHistoryDto
{
    public int      Id           { get; set; }
    public string   OwnerName    { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public DateTime? EndDate     { get; set; }
    public string   DocumentType { get; set; } = string.Empty;
    public string   DocumentRef  { get; set; } = string.Empty;
    public string   TransferType { get; set; } = string.Empty;
    public bool     IsVerified   { get; set; }
    public string?  Notes        { get; set; }
}

// ── Risk Report DTOs ──────────────────────────────────────
public class RiskReportDto
{
    public int           Id          { get; set; }
    public int           LandRecordId{ get; set; }
    public string        RiskLevel   { get; set; } = string.Empty;
    public int           RiskScore   { get; set; }
    public DateTime      GeneratedAt { get; set; }
    public string?       Summary     { get; set; }
    public List<RiskFlagDto> Flags   { get; set; } = new();
    public LandRecordResponseDto? LandRecord { get; set; }
}

public class RiskFlagDto
{
    public string FlagType    { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity    { get; set; } = string.Empty;
}

public class RiskSummaryDto
{
    public string RiskLevel { get; set; } = string.Empty;
    public int    RiskScore { get; set; }
    public int    FlagCount { get; set; }
}

// ── Verification Request DTOs ─────────────────────────────
public record CreateVerificationRequestDto(
    string RequestedBy,
    UserRole UserRole,
    int LandRecordId,
    string? Purpose
);

public class VerificationRequestResponseDto
{
    public int      Id           { get; set; }
    public string   RequestedBy  { get; set; } = string.Empty;
    public string   UserRole     { get; set; } = string.Empty;
    public int      LandRecordId { get; set; }
    public DateTime RequestedAt  { get; set; }
    public string?  Purpose      { get; set; }
    public string   Status       { get; set; } = string.Empty;
    public RiskReportDto? RiskReport { get; set; }
}

// ── Dashboard / Analytics DTOs ────────────────────────────
public class DashboardStatsDto
{
    public int TotalRecords      { get; set; }
    public int VerifiedRecords   { get; set; }
    public int FlaggedRecords    { get; set; }
    public int PendingRecords    { get; set; }
    public int LowRiskCount      { get; set; }
    public int MediumRiskCount   { get; set; }
    public int HighRiskCount     { get; set; }
    public int CriticalRiskCount { get; set; }
    public int TotalDocuments    { get; set; }
    public int TotalVerificationRequests { get; set; }
    public List<MonthlyStat> MonthlyVerifications { get; set; } = new();
}

public class MonthlyStat
{
    public string Month { get; set; } = string.Empty;
    public int    Count { get; set; }
}

// ── Search DTO ────────────────────────────────────────────
public class SearchDto
{
    public string?      Query        { get; set; }
    public string?      Village      { get; set; }
    public string?      District     { get; set; }
    public RiskLevel?   RiskLevel    { get; set; }
    public RecordStatus? Status      { get; set; }
    public int          Page         { get; set; } = 1;
    public int          PageSize     { get; set; } = 20;
}

public class PagedResultDto<T>
{
    public List<T> Items       { get; set; } = new();
    public int     TotalCount  { get; set; }
    public int     Page        { get; set; }
    public int     PageSize    { get; set; }
    public int     TotalPages  => (int)Math.Ceiling((double)TotalCount / PageSize);
}
