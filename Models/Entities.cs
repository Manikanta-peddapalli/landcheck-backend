// ============================================================
//  LandCheck – Domain Models
//  File: Models/Entities.cs
// ============================================================

namespace LandCheck.API.Models;

// ── Land Record ────────────────────────────────────────────
public class LandRecord
{
    public int    Id            { get; set; }
    public string SurveyNumber  { get; set; } = string.Empty;
    public string Village       { get; set; } = string.Empty;
    public string Mandal        { get; set; } = string.Empty;
    public string District      { get; set; } = string.Empty;
    public string State         { get; set; } = "Andhra Pradesh";
    public decimal ExtentAcres  { get; set; }
    public string LandType      { get; set; } = string.Empty;   // Agricultural / Residential / Commercial
    public string CurrentOwner  { get; set; } = string.Empty;
    public string AadhaarRef    { get; set; } = string.Empty;   // masked ref only
    public RecordStatus Status  { get; set; } = RecordStatus.Pending;
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt   { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<OwnershipHistory> OwnershipHistories { get; set; } = new();
    public List<LandDocument>     Documents          { get; set; } = new();
    public RiskReport?            LatestRiskReport   { get; set; }
}

public enum RecordStatus { Pending, UnderReview, Verified, Flagged, Disputed }

// ── Ownership History ──────────────────────────────────────
public class OwnershipHistory
{
    public int      Id           { get; set; }
    public int      LandRecordId { get; set; }
    public string   OwnerName    { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public DateTime? EndDate     { get; set; }         // null = current owner
    public string   DocumentType { get; set; } = string.Empty;
    public string   DocumentRef  { get; set; } = string.Empty;
    public TransferType TransferType { get; set; }
    public bool     IsVerified   { get; set; }
    public string?  Notes        { get; set; }

    public LandRecord LandRecord { get; set; } = null!;
}

public enum TransferType { ManualRegister, RegisteredSaleDeed, OnlineTransfer, CourtOrder, Inheritance, Unknown }

// ── Document ───────────────────────────────────────────────
public class LandDocument
{
    public int          Id           { get; set; }
    public int          LandRecordId { get; set; }
    public DocumentType DocumentType { get; set; }
    public string       FileName     { get; set; } = string.Empty;
    public string       StoragePath  { get; set; } = string.Empty;
    public long         FileSizeBytes{ get; set; }
    public string       MimeType     { get; set; } = string.Empty;
    public DateTime     UploadedAt   { get; set; } = DateTime.UtcNow;
    public bool         IsProcessed  { get; set; }
    public string?      ExtractedText{ get; set; }

    public LandRecord LandRecord { get; set; } = null!;
    public List<ExtractedField> ExtractedFields { get; set; } = new();
}

public enum DocumentType { SaleDeed, EC, ROR1B, Adangal, Passbook, SLRRegister, CourtOrder, WeblandRecord, Other }

// ── Extracted Field ────────────────────────────────────────
public class ExtractedField
{
    public int    Id               { get; set; }
    public int    LandDocumentId   { get; set; }
    public string FieldName        { get; set; } = string.Empty;
    public string ExtractedValue   { get; set; } = string.Empty;
    public float  Confidence       { get; set; }

    public LandDocument LandDocument { get; set; } = null!;
}

// ── Risk Report ────────────────────────────────────────────
public class RiskReport
{
    public int       Id           { get; set; }
    public int       LandRecordId { get; set; }
    public RiskLevel RiskLevel    { get; set; }
    public int       RiskScore    { get; set; }   // 0–100
    public DateTime  GeneratedAt  { get; set; } = DateTime.UtcNow;
    public string?   Summary      { get; set; }

    public LandRecord LandRecord { get; set; } = null!;
    public List<RiskFlag> Flags  { get; set; } = new();
}

public enum RiskLevel { Low, Medium, High, Critical }

// ── Risk Flag ──────────────────────────────────────────────
public class RiskFlag
{
    public int      Id           { get; set; }
    public int      RiskReportId { get; set; }
    public FlagType FlagType     { get; set; }
    public string   Description  { get; set; } = string.Empty;
    public Severity Severity     { get; set; }

    public RiskReport RiskReport { get; set; } = null!;
}

public enum FlagType
{
    OwnerNameMismatch,
    SurveyNumberMismatch,
    AreaMismatch,
    MissingOwnershipLink,
    DuplicateTransaction,
    CancellationOrder,
    CourtDisputePending,
    DocumentAuthenticity,
    ECMismatch,
    PassbookMismatch
}

public enum Severity { Low, Medium, High, Critical }

// ── Verification Request (from users) ─────────────────────
public class VerificationRequest
{
    public int      Id           { get; set; }
    public string   RequestedBy  { get; set; } = string.Empty;
    public UserRole UserRole     { get; set; }
    public int      LandRecordId { get; set; }
    public DateTime RequestedAt  { get; set; } = DateTime.UtcNow;
    public string?  Purpose      { get; set; }
    public RequestStatus Status  { get; set; } = RequestStatus.Pending;
    public int?     RiskReportId { get; set; }

    public LandRecord  LandRecord  { get; set; } = null!;
    public RiskReport? RiskReport  { get; set; }
}

public enum UserRole { Farmer, Bank, Lawyer, RealEstateAgent, NRI, Government }
public enum RequestStatus { Pending, Processing, Completed, Failed }
