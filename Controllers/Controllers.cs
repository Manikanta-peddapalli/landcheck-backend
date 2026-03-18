// ============================================================
//  LandCheck – API Controllers
//  File: Controllers/Controllers.cs
// ============================================================

using LandCheck.API.Models;
using LandCheck.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LandCheck.API.Controllers;

// ─────────────────────────────────────────────────────────────
//  LAND RECORDS CONTROLLER
// ─────────────────────────────────────────────────────────────

[ApiController]
[Route("api/[controller]")]
public class LandRecordsController : ControllerBase
{
    private readonly ILandRecordService _svc;
    public LandRecordsController(ILandRecordService svc) => _svc = svc;

    /// <summary>Search land records with filters</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<LandRecordResponseDto>>> Search([FromQuery] SearchDto search)
        => Ok(await _svc.SearchAsync(search));

    /// <summary>Get land record by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<LandRecordResponseDto>> GetById(int id)
    {
        var record = await _svc.GetByIdAsync(id);
        return record is null ? NotFound($"Record {id} not found") : Ok(record);
    }

    /// <summary>Get land record by survey number</summary>
    [HttpGet("survey/{surveyNumber}")]
    public async Task<ActionResult<LandRecordResponseDto>> GetBySurveyNumber(string surveyNumber)
    {
        var record = await _svc.GetBySurveyNumberAsync(surveyNumber);
        return record is null ? NotFound($"Survey number {surveyNumber} not found") : Ok(record);
    }

    /// <summary>Create a new land record</summary>
    [HttpPost]
    public async Task<ActionResult<LandRecordResponseDto>> Create([FromBody] CreateLandRecordDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _svc.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Update an existing land record</summary>
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<LandRecordResponseDto>> Update(int id, [FromBody] UpdateLandRecordDto dto)
    {
        var updated = await _svc.UpdateAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Delete a land record</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _svc.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Get full ownership chain for a land record</summary>
    [HttpGet("{id:int}/ownership-chain")]
    public async Task<ActionResult<List<OwnershipHistoryDto>>> GetOwnershipChain(int id)
        => Ok(await _svc.GetOwnershipChainAsync(id));

    /// <summary>Add a new entry to the ownership history</summary>
    [HttpPost("{id:int}/ownership-chain")]
    public async Task<ActionResult<OwnershipHistoryDto>> AddOwnershipHistory(int id, [FromBody] CreateOwnershipHistoryDto dto)
    {
        if (dto.LandRecordId != id) return BadRequest("LandRecordId mismatch");
        var result = await _svc.AddOwnershipHistoryAsync(dto);
        return Ok(result);
    }
}

// ─────────────────────────────────────────────────────────────
//  RISK ANALYSIS CONTROLLER
// ─────────────────────────────────────────────────────────────

[ApiController]
[Route("api/[controller]")]
public class RiskAnalysisController : ControllerBase
{
    private readonly IRiskAnalysisService _svc;
    public RiskAnalysisController(IRiskAnalysisService svc) => _svc = svc;

    /// <summary>Run full risk analysis on a land record</summary>
    [HttpPost("analyze/{landRecordId:int}")]
    public async Task<ActionResult<RiskReportDto>> Analyze(int landRecordId)
    {
        try
        {
            var report = await _svc.AnalyzeAsync(landRecordId);
            return Ok(report);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>Get latest risk report for a land record</summary>
    [HttpGet("report/{landRecordId:int}")]
    public async Task<ActionResult<RiskReportDto>> GetLatestReport(int landRecordId)
    {
        var report = await _svc.GetLatestReportAsync(landRecordId);
        return report is null ? NotFound("No risk report found for this record") : Ok(report);
    }

    /// <summary>Get dashboard analytics stats</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboard()
        => Ok(await _svc.GetDashboardStatsAsync());
}

// ─────────────────────────────────────────────────────────────
//  DOCUMENTS CONTROLLER
// ─────────────────────────────────────────────────────────────

[ApiController]
[Route("api/land-records/{landRecordId:int}/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _svc;
    public DocumentsController(IDocumentService svc) => _svc = svc;

    /// <summary>Upload a land document</summary>
    [HttpPost]
    [RequestSizeLimit(20_000_000)] // 20 MB
    public async Task<IActionResult> Upload(int landRecordId, IFormFile file, [FromQuery] DocumentType docType = DocumentType.Other)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded");

        var allowed = new[] { "application/pdf", "image/jpeg", "image/png", "image/tiff" };
        if (!allowed.Contains(file.ContentType))
            return BadRequest("Only PDF, JPEG, PNG, and TIFF files are accepted");

        var doc = await _svc.SaveDocumentAsync(landRecordId, file, docType);
        return Ok(new { doc.Id, doc.FileName, doc.DocumentType, doc.FileSizeBytes });
    }

    /// <summary>Get all documents for a land record</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(int landRecordId)
    {
        var docs = await _svc.GetDocumentsAsync(landRecordId);
        return Ok(docs.Select(d => new { d.Id, d.FileName, d.DocumentType, d.FileSizeBytes, d.IsProcessed, d.UploadedAt }));
    }

    /// <summary>Delete a document</summary>
    [HttpDelete("{docId:int}")]
    public async Task<IActionResult> Delete(int landRecordId, int docId)
    {
        var deleted = await _svc.DeleteDocumentAsync(docId);
        return deleted ? NoContent() : NotFound();
    }
}

// ─────────────────────────────────────────────────────────────
//  REPORTS CONTROLLER
// ─────────────────────────────────────────────────────────────

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _svc;
    public ReportsController(IReportService svc) => _svc = svc;

    /// <summary>Download PDF risk report</summary>
    [HttpGet("{landRecordId:int}/pdf")]
    public async Task<IActionResult> DownloadPdf(int landRecordId)
    {
        var bytes = await _svc.GeneratePdfReportAsync(landRecordId);
        if (bytes.Length == 0) return NotFound("No report available");
        return File(bytes, "application/pdf", $"LandCheck-Report-{landRecordId}.pdf");
    }
}
