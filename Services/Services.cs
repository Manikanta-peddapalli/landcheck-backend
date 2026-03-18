// ============================================================
//  LandCheck – Service Layer
//  File: Services/Services.cs
// ============================================================

using LandCheck.API.Data;
using LandCheck.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LandCheck.API.Services;

// ─────────────────────────────────────────────────────────────
//  INTERFACES
// ─────────────────────────────────────────────────────────────

public interface ILandRecordService
{
    Task<PagedResultDto<LandRecordResponseDto>> SearchAsync(SearchDto search);
    Task<LandRecordResponseDto?> GetByIdAsync(int id);
    Task<LandRecordResponseDto?> GetBySurveyNumberAsync(string surveyNumber);
    Task<LandRecordResponseDto>  CreateAsync(CreateLandRecordDto dto);
    Task<LandRecordResponseDto?> UpdateAsync(int id, UpdateLandRecordDto dto);
    Task<bool>                   DeleteAsync(int id);
    Task<List<OwnershipHistoryDto>> GetOwnershipChainAsync(int landRecordId);
    Task<OwnershipHistoryDto>    AddOwnershipHistoryAsync(CreateOwnershipHistoryDto dto);
}

public interface IRiskAnalysisService
{
    Task<RiskReportDto>  AnalyzeAsync(int landRecordId);
    Task<RiskReportDto?> GetLatestReportAsync(int landRecordId);
    Task<DashboardStatsDto> GetDashboardStatsAsync();
}

public interface IDocumentService
{
    Task<LandDocument> SaveDocumentAsync(int landRecordId, IFormFile file, DocumentType docType);
    Task<List<LandDocument>> GetDocumentsAsync(int landRecordId);
    Task<bool> DeleteDocumentAsync(int id);
}

public interface IReportService
{
    Task<byte[]> GeneratePdfReportAsync(int landRecordId);
}

// ─────────────────────────────────────────────────────────────
//  LAND RECORD SERVICE
// ─────────────────────────────────────────────────────────────

public class LandRecordService : ILandRecordService
{
    private readonly LandCheckDbContext _db;
    public LandRecordService(LandCheckDbContext db) => _db = db;

    public async Task<PagedResultDto<LandRecordResponseDto>> SearchAsync(SearchDto search)
    {
        var query = _db.LandRecords
            .Include(x => x.LatestRiskReport)
            .Include(x => x.OwnershipHistories)
            .Include(x => x.Documents)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var q = search.Query.ToLower();
            query = query.Where(x =>
                x.SurveyNumber.ToLower().Contains(q) ||
                x.Village.ToLower().Contains(q)      ||
                x.CurrentOwner.ToLower().Contains(q) ||
                x.District.ToLower().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(search.Village))
            query = query.Where(x => x.Village.ToLower().Contains(search.Village.ToLower()));

        if (!string.IsNullOrWhiteSpace(search.District))
            query = query.Where(x => x.District.ToLower().Contains(search.District.ToLower()));

        if (search.Status.HasValue)
            query = query.Where(x => x.Status == search.Status.Value);

        if (search.RiskLevel.HasValue)
            query = query.Where(x => x.LatestRiskReport != null && x.LatestRiskReport.RiskLevel == search.RiskLevel.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync();

        return new PagedResultDto<LandRecordResponseDto>
        {
            Items      = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page       = search.Page,
            PageSize   = search.PageSize
        };
    }

    public async Task<LandRecordResponseDto?> GetByIdAsync(int id)
    {
        var record = await _db.LandRecords
            .Include(x => x.LatestRiskReport).ThenInclude(r => r!.Flags)
            .Include(x => x.OwnershipHistories)
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id);
        return record is null ? null : MapToDto(record);
    }

    public async Task<LandRecordResponseDto?> GetBySurveyNumberAsync(string surveyNumber)
    {
        var record = await _db.LandRecords
            .Include(x => x.LatestRiskReport).ThenInclude(r => r!.Flags)
            .Include(x => x.OwnershipHistories)
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.SurveyNumber == surveyNumber);
        return record is null ? null : MapToDto(record);
    }

    public async Task<LandRecordResponseDto> CreateAsync(CreateLandRecordDto dto)
    {
        var record = new LandRecord
        {
            SurveyNumber = dto.SurveyNumber,
            Village      = dto.Village,
            Mandal       = dto.Mandal,
            District     = dto.District,
            State        = dto.State,
            ExtentAcres  = dto.ExtentAcres,
            LandType     = dto.LandType,
            CurrentOwner = dto.CurrentOwner,
        };
        _db.LandRecords.Add(record);
        await _db.SaveChangesAsync();

        // Add current owner as first history entry
        _db.OwnershipHistories.Add(new OwnershipHistory
        {
            LandRecordId = record.Id,
            OwnerName    = dto.CurrentOwner,
            TransferDate = DateTime.UtcNow,
            TransferType = TransferType.Unknown,
            IsVerified   = false,
            DocumentType = "New Record",
            DocumentRef  = ""
        });
        await _db.SaveChangesAsync();
        return MapToDto(record);
    }

    public async Task<LandRecordResponseDto?> UpdateAsync(int id, UpdateLandRecordDto dto)
    {
        var record = await _db.LandRecords.FindAsync(id);
        if (record is null) return null;

        if (dto.Village      is not null) record.Village      = dto.Village;
        if (dto.Mandal       is not null) record.Mandal       = dto.Mandal;
        if (dto.District     is not null) record.District     = dto.District;
        if (dto.ExtentAcres  is not null) record.ExtentAcres  = dto.ExtentAcres.Value;
        if (dto.LandType     is not null) record.LandType     = dto.LandType;
        if (dto.CurrentOwner is not null) record.CurrentOwner = dto.CurrentOwner;
        if (dto.Status       is not null) record.Status       = dto.Status.Value;
        record.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var record = await _db.LandRecords.FindAsync(id);
        if (record is null) return false;
        _db.LandRecords.Remove(record);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<OwnershipHistoryDto>> GetOwnershipChainAsync(int landRecordId)
    {
        var histories = await _db.OwnershipHistories
            .Where(x => x.LandRecordId == landRecordId)
            .OrderBy(x => x.TransferDate)
            .ToListAsync();
        return histories.Select(MapHistoryToDto).ToList();
    }

    public async Task<OwnershipHistoryDto> AddOwnershipHistoryAsync(CreateOwnershipHistoryDto dto)
    {
        var history = new OwnershipHistory
        {
            LandRecordId = dto.LandRecordId,
            OwnerName    = dto.OwnerName,
            TransferDate = dto.TransferDate,
            EndDate      = dto.EndDate,
            DocumentType = dto.DocumentType,
            DocumentRef  = dto.DocumentRef,
            TransferType = dto.TransferType,
            IsVerified   = dto.IsVerified,
            Notes        = dto.Notes
        };
        _db.OwnershipHistories.Add(history);
        await _db.SaveChangesAsync();
        return MapHistoryToDto(history);
    }

    // ── Mappers ─────────────────────────────────────────────
    private static LandRecordResponseDto MapToDto(LandRecord r) => new()
    {
        Id             = r.Id,
        SurveyNumber   = r.SurveyNumber,
        Village        = r.Village,
        Mandal         = r.Mandal,
        District       = r.District,
        State          = r.State,
        ExtentAcres    = r.ExtentAcres,
        LandType       = r.LandType,
        CurrentOwner   = r.CurrentOwner,
        Status         = r.Status.ToString(),
        CreatedAt      = r.CreatedAt,
        DocumentCount  = r.Documents?.Count ?? 0,
        RiskSummary    = r.LatestRiskReport is null ? null : new RiskSummaryDto
        {
            RiskLevel = r.LatestRiskReport.RiskLevel.ToString(),
            RiskScore = r.LatestRiskReport.RiskScore,
            FlagCount = r.LatestRiskReport.Flags?.Count ?? 0
        },
        OwnershipChain = r.OwnershipHistories?
            .OrderBy(x => x.TransferDate)
            .Select(MapHistoryToDto)
            .ToList() ?? new()
    };

    private static OwnershipHistoryDto MapHistoryToDto(OwnershipHistory h) => new()
    {
        Id           = h.Id,
        OwnerName    = h.OwnerName,
        TransferDate = h.TransferDate,
        EndDate      = h.EndDate,
        DocumentType = h.DocumentType,
        DocumentRef  = h.DocumentRef,
        TransferType = h.TransferType.ToString(),
        IsVerified   = h.IsVerified,
        Notes        = h.Notes
    };
}

// ─────────────────────────────────────────────────────────────
//  RISK ANALYSIS SERVICE
// ─────────────────────────────────────────────────────────────

public class RiskAnalysisService : IRiskAnalysisService
{
    private readonly LandCheckDbContext _db;
    public RiskAnalysisService(LandCheckDbContext db) => _db = db;

    public async Task<RiskReportDto> AnalyzeAsync(int landRecordId)
    {
        var record = await _db.LandRecords
            .Include(x => x.OwnershipHistories)
            .Include(x => x.Documents).ThenInclude(d => d.ExtractedFields)
            .FirstOrDefaultAsync(x => x.Id == landRecordId)
            ?? throw new KeyNotFoundException($"LandRecord {landRecordId} not found");

        var flags = new List<RiskFlag>();
        int score = 0;

        // ── Rule 1: Chain Breaks ─────────────────────────
        var chain = record.OwnershipHistories.OrderBy(x => x.TransferDate).ToList();
        for (int i = 0; i < chain.Count - 1; i++)
        {
            var curr = chain[i];
            var next = chain[i + 1];
            if (curr.EndDate.HasValue && (next.TransferDate - curr.EndDate.Value).TotalDays > 30)
            {
                flags.Add(new RiskFlag
                {
                    FlagType    = FlagType.MissingOwnershipLink,
                    Description = $"Gap in ownership chain: {curr.EndDate:yyyy-MM-dd} to {next.TransferDate:yyyy-MM-dd} ({(next.TransferDate - curr.EndDate.Value).Days} days unaccounted)",
                    Severity    = Severity.Critical
                });
                score += 35;
            }
        }

        // ── Rule 2: Unknown/Unverified Entries ───────────
        var unknownLinks = chain.Where(x => !x.IsVerified || x.TransferType == TransferType.Unknown).ToList();
        if (unknownLinks.Any())
        {
            flags.Add(new RiskFlag
            {
                FlagType    = FlagType.DocumentAuthenticity,
                Description = $"{unknownLinks.Count} unverified ownership record(s) in the chain",
                Severity    = Severity.Medium
            });
            score += unknownLinks.Count * 10;
        }

        // ── Rule 3: Extent Cross-check ───────────────────
        var extentFields = record.Documents
            .SelectMany(d => d.ExtractedFields)
            .Where(f => f.FieldName.Equals("Extent", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var distinctExtents = extentFields.Select(f => f.ExtractedValue).Distinct().ToList();
        if (distinctExtents.Count > 1)
        {
            flags.Add(new RiskFlag
            {
                FlagType    = FlagType.AreaMismatch,
                Description = $"Area mismatch across documents: {string.Join(" vs ", distinctExtents)}",
                Severity    = Severity.High
            });
            score += 20;
        }

        // ── Rule 4: Owner Name Cross-check ───────────────
        var ownerFields = record.Documents
            .SelectMany(d => d.ExtractedFields)
            .Where(f => f.FieldName.Equals("OwnerName", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var distinctOwners = ownerFields.Select(f => f.ExtractedValue.ToLower().Trim()).Distinct().ToList();
        if (distinctOwners.Count > 1)
        {
            flags.Add(new RiskFlag
            {
                FlagType    = FlagType.OwnerNameMismatch,
                Description = $"Owner name differs across documents: {string.Join(", ", distinctOwners.Select(o => $"'{o}'"))}",
                Severity    = Severity.High
            });
            score += 20;
        }

        // ── Rule 5: Survey Number Consistency ────────────
        var surveyFields = record.Documents
            .SelectMany(d => d.ExtractedFields)
            .Where(f => f.FieldName.Equals("SurveyNumber", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var distinctSurveys = surveyFields.Select(f => f.ExtractedValue).Distinct().ToList();
        if (distinctSurveys.Count > 1)
        {
            flags.Add(new RiskFlag
            {
                FlagType    = FlagType.SurveyNumberMismatch,
                Description = $"Survey number inconsistency: {string.Join(", ", distinctSurveys)}",
                Severity    = Severity.High
            });
            score += 15;
        }

        // ── Compute Final Level ───────────────────────────
        score = Math.Min(score, 100);
        var level = score switch
        {
            <= 20 => RiskLevel.Low,
            <= 45 => RiskLevel.Medium,
            <= 70 => RiskLevel.High,
            _     => RiskLevel.Critical
        };

        var summary = level switch
        {
            RiskLevel.Low      => "Clean ownership chain. No major issues detected. Safe to proceed with standard due diligence.",
            RiskLevel.Medium   => $"Minor inconsistencies found ({flags.Count} flag(s)). Recommend verifying specific documents before transaction.",
            RiskLevel.High     => $"Significant issues detected ({flags.Count} flag(s)). Do NOT proceed without thorough legal verification.",
            RiskLevel.Critical => $"CRITICAL RISK: {flags.Count} major flag(s). Potential fraud indicators. Immediate legal review required.",
            _                  => ""
        };

        // ── Save or update report ─────────────────────────
        var existing = await _db.RiskReports.Include(r => r.Flags)
                                            .FirstOrDefaultAsync(r => r.LandRecordId == landRecordId);
        if (existing is not null)
        {
            _db.RiskFlags.RemoveRange(existing.Flags);
            existing.RiskLevel   = level;
            existing.RiskScore   = score;
            existing.GeneratedAt = DateTime.UtcNow;
            existing.Summary     = summary;
            existing.Flags       = flags;
        }
        else
        {
            var report = new RiskReport
            {
                LandRecordId = landRecordId,
                RiskLevel    = level,
                RiskScore    = score,
                Summary      = summary,
                Flags        = flags
            };
            _db.RiskReports.Add(report);
        }

        // Update record status
        record.Status = level >= RiskLevel.High ? RecordStatus.Flagged : RecordStatus.Verified;
        await _db.SaveChangesAsync();

        return await GetLatestReportAsync(landRecordId)
               ?? throw new InvalidOperationException("Failed to retrieve generated report");
    }

    public async Task<RiskReportDto?> GetLatestReportAsync(int landRecordId)
    {
        var report = await _db.RiskReports
            .Include(r => r.Flags)
            .Include(r => r.LandRecord).ThenInclude(lr => lr.OwnershipHistories)
            .FirstOrDefaultAsync(r => r.LandRecordId == landRecordId);

        if (report is null) return null;

        return new RiskReportDto
        {
            Id           = report.Id,
            LandRecordId = report.LandRecordId,
            RiskLevel    = report.RiskLevel.ToString(),
            RiskScore    = report.RiskScore,
            GeneratedAt  = report.GeneratedAt,
            Summary      = report.Summary,
            Flags        = report.Flags.Select(f => new RiskFlagDto
            {
                FlagType    = f.FlagType.ToString(),
                Description = f.Description,
                Severity    = f.Severity.ToString()
            }).ToList()
        };
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        var stats = new DashboardStatsDto
        {
            TotalRecords    = await _db.LandRecords.CountAsync(),
            VerifiedRecords = await _db.LandRecords.CountAsync(x => x.Status == RecordStatus.Verified),
            FlaggedRecords  = await _db.LandRecords.CountAsync(x => x.Status == RecordStatus.Flagged),
            PendingRecords  = await _db.LandRecords.CountAsync(x => x.Status == RecordStatus.Pending),
            TotalDocuments  = await _db.LandDocuments.CountAsync(),
            TotalVerificationRequests = await _db.VerificationRequests.CountAsync(),
            LowRiskCount    = await _db.RiskReports.CountAsync(x => x.RiskLevel == RiskLevel.Low),
            MediumRiskCount = await _db.RiskReports.CountAsync(x => x.RiskLevel == RiskLevel.Medium),
            HighRiskCount   = await _db.RiskReports.CountAsync(x => x.RiskLevel == RiskLevel.High),
            CriticalRiskCount = await _db.RiskReports.CountAsync(x => x.RiskLevel == RiskLevel.Critical),
        };
        return stats;
    }
}

// ─────────────────────────────────────────────────────────────
//  DOCUMENT SERVICE
// ─────────────────────────────────────────────────────────────

public class DocumentService : IDocumentService
{
    private readonly LandCheckDbContext _db;
    private readonly IWebHostEnvironment _env;

    public DocumentService(LandCheckDbContext db, IWebHostEnvironment env)
    {
        _db  = db;
        _env = env;
    }

    public async Task<LandDocument> SaveDocumentAsync(int landRecordId, IFormFile file, DocumentType docType)
    {
        var uploadPath = Path.Combine(_env.ContentRootPath, "uploads", landRecordId.ToString());
        Directory.CreateDirectory(uploadPath);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadPath, fileName);

        using (var stream = File.Create(filePath))
            await file.CopyToAsync(stream);

        var doc = new LandDocument
        {
            LandRecordId  = landRecordId,
            DocumentType  = docType,
            FileName      = file.FileName,
            StoragePath   = filePath,
            FileSizeBytes = file.Length,
            MimeType      = file.ContentType,
            IsProcessed   = false
        };

        _db.LandDocuments.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    public async Task<List<LandDocument>> GetDocumentsAsync(int landRecordId)
        => await _db.LandDocuments.Where(d => d.LandRecordId == landRecordId).ToListAsync();

    public async Task<bool> DeleteDocumentAsync(int id)
    {
        var doc = await _db.LandDocuments.FindAsync(id);
        if (doc is null) return false;
        if (File.Exists(doc.StoragePath)) File.Delete(doc.StoragePath);
        _db.LandDocuments.Remove(doc);
        await _db.SaveChangesAsync();
        return true;
    }
}

// ─────────────────────────────────────────────────────────────
//  REPORT SERVICE (placeholder – real PDF generation below)
// ─────────────────────────────────────────────────────────────

public class ReportService : IReportService
{
    private readonly LandCheckDbContext _db;
    public ReportService(LandCheckDbContext db) => _db = db;

    public async Task<byte[]> GeneratePdfReportAsync(int landRecordId)
    {
        // Placeholder: returns minimal PDF bytes
        // In production: use iTextSharp / QuestPDF to build a proper report
        var record = await _db.LandRecords
            .Include(r => r.LatestRiskReport).ThenInclude(rr => rr!.Flags)
            .Include(r => r.OwnershipHistories)
            .FirstOrDefaultAsync(r => r.Id == landRecordId);

        if (record is null) return Array.Empty<byte>();

        // Return empty array as stub; integrate QuestPDF / iTextSharp for real PDF
        return Array.Empty<byte>();
    }
}
