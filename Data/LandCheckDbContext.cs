// ============================================================
//  LandCheck – Database Context
//  File: Data/LandCheckDbContext.cs
// ============================================================

using LandCheck.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LandCheck.API.Data;

public class LandCheckDbContext : DbContext
{
    public LandCheckDbContext(DbContextOptions<LandCheckDbContext> options) : base(options) { }

    public DbSet<LandRecord>           LandRecords           { get; set; }
    public DbSet<OwnershipHistory>     OwnershipHistories    { get; set; }
    public DbSet<LandDocument>         LandDocuments         { get; set; }
    public DbSet<ExtractedField>       ExtractedFields       { get; set; }
    public DbSet<RiskReport>           RiskReports           { get; set; }
    public DbSet<RiskFlag>             RiskFlags             { get; set; }
    public DbSet<VerificationRequest>  VerificationRequests  { get; set; }
    public DbSet<AppUser> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── LandRecord ─────────────────────────────────────
        modelBuilder.Entity<LandRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SurveyNumber).IsRequired().HasMaxLength(50);
            e.Property(x => x.Village).IsRequired().HasMaxLength(100);
            e.Property(x => x.Mandal).IsRequired().HasMaxLength(100);
            e.Property(x => x.District).IsRequired().HasMaxLength(100);
            e.Property(x => x.CurrentOwner).IsRequired().HasMaxLength(200);
            e.Property(x => x.ExtentAcres).HasPrecision(10, 4);
            e.HasIndex(x => x.SurveyNumber).IsUnique();

            e.HasMany(x => x.OwnershipHistories)
             .WithOne(x => x.LandRecord)
             .HasForeignKey(x => x.LandRecordId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Documents)
             .WithOne(x => x.LandRecord)
             .HasForeignKey(x => x.LandRecordId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.LatestRiskReport)
             .WithOne(x => x.LandRecord)
             .HasForeignKey<RiskReport>(x => x.LandRecordId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── OwnershipHistory ───────────────────────────────
        modelBuilder.Entity<OwnershipHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OwnerName).IsRequired().HasMaxLength(200);
            e.Property(x => x.DocumentRef).HasMaxLength(100);
        });

        // ── LandDocument ───────────────────────────────────
        modelBuilder.Entity<LandDocument>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).IsRequired().HasMaxLength(260);
            e.Property(x => x.StoragePath).HasMaxLength(500);

            e.HasMany(x => x.ExtractedFields)
             .WithOne(x => x.LandDocument)
             .HasForeignKey(x => x.LandDocumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── RiskReport ─────────────────────────────────────
        modelBuilder.Entity<RiskReport>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Flags)
             .WithOne(x => x.RiskReport)
             .HasForeignKey(x => x.RiskReportId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── VerificationRequest ────────────────────────────
        modelBuilder.Entity<VerificationRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.LandRecord)
             .WithMany()
             .HasForeignKey(x => x.LandRecordId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Seed Data ──────────────────────────────────────
        SeedData(modelBuilder);
        AuthDbExtension.ConfigureAuthEntities(modelBuilder);
    }

    private static void SeedData(ModelBuilder m)
    {
        m.Entity<LandRecord>().HasData(
            new LandRecord { Id=1, SurveyNumber="SY-2024-001", Village="Nellore", Mandal="Kovur", District="SPSR Nellore", ExtentAcres=2.50m, LandType="Agricultural", CurrentOwner="Ravi Kumar Reddy", Status=RecordStatus.Verified, CreatedAt=new DateTime(2024,1,10), UpdatedAt=new DateTime(2024,1,10) },
            new LandRecord { Id=2, SurveyNumber="SY-2023-045", Village="Vijayawada", Mandal="Krishna", District="Krishna", ExtentAcres=1.20m, LandType="Residential Plot", CurrentOwner="Lakshmi Devi", Status=RecordStatus.Flagged, CreatedAt=new DateTime(2023,6,15), UpdatedAt=new DateTime(2023,6,15) },
            new LandRecord { Id=3, SurveyNumber="SY-2022-112", Village="Guntur", Mandal="Tenali", District="Guntur", ExtentAcres=3.75m, LandType="Agricultural", CurrentOwner="Venkata Subba Rao", Status=RecordStatus.UnderReview, CreatedAt=new DateTime(2022,3,20), UpdatedAt=new DateTime(2022,3,20) }
        );

        m.Entity<OwnershipHistory>().HasData(
            // Record 1 - clean chain
            new OwnershipHistory { Id=1, LandRecordId=1, OwnerName="Gopal Rao", TransferDate=new DateTime(1985,1,1), EndDate=new DateTime(2001,6,10), DocumentType="Manual Register", DocumentRef="MR-1985/441", TransferType=TransferType.ManualRegister, IsVerified=true },
            new OwnershipHistory { Id=2, LandRecordId=1, OwnerName="Suresh Rao", TransferDate=new DateTime(2001,6,10), EndDate=new DateTime(2015,9,20), DocumentType="Sale Deed", DocumentRef="SD-4521/2001", TransferType=TransferType.RegisteredSaleDeed, IsVerified=true },
            new OwnershipHistory { Id=3, LandRecordId=1, OwnerName="Ravi Kumar Reddy", TransferDate=new DateTime(2015,9,20), EndDate=null, DocumentType="EC", DocumentRef="EC-2015/778", TransferType=TransferType.OnlineTransfer, IsVerified=true },
            // Record 2 - broken chain
            new OwnershipHistory { Id=4, LandRecordId=2, OwnerName="Ramaiah", TransferDate=new DateTime(1990,1,1), EndDate=new DateTime(2005,3,15), DocumentType="SLR Record", DocumentRef="SLR-1990/112", TransferType=TransferType.ManualRegister, IsVerified=true },
            new OwnershipHistory { Id=5, LandRecordId=2, OwnerName="UNKNOWN", TransferDate=new DateTime(2005,3,15), EndDate=new DateTime(2012,7,1), DocumentType="Missing", DocumentRef="", TransferType=TransferType.Unknown, IsVerified=false, Notes="CHAIN BREAK: No record found for this period" },
            new OwnershipHistory { Id=6, LandRecordId=2, OwnerName="Lakshmi Devi", TransferDate=new DateTime(2012,7,1), EndDate=null, DocumentType="Sale Deed", DocumentRef="SD-9012/2012", TransferType=TransferType.RegisteredSaleDeed, IsVerified=false },
            // Record 3
            new OwnershipHistory { Id=7, LandRecordId=3, OwnerName="Hanumaiah", TransferDate=new DateTime(1978,1,1), EndDate=new DateTime(1999,4,5), DocumentType="Adangal", DocumentRef="ADG-1978/221", TransferType=TransferType.ManualRegister, IsVerified=true },
            new OwnershipHistory { Id=8, LandRecordId=3, OwnerName="Venkata Subba Rao", TransferDate=new DateTime(1999,4,5), EndDate=null, DocumentType="Passbook", DocumentRef="PB-5541/1999", TransferType=TransferType.RegisteredSaleDeed, IsVerified=true }
        );

        m.Entity<RiskReport>().HasData(
            new RiskReport { Id=1, LandRecordId=1, RiskLevel=RiskLevel.Low, RiskScore=12, GeneratedAt=new DateTime(2024,1,11), Summary="Clean ownership chain with verified documents. No major risks detected." },
            new RiskReport { Id=2, LandRecordId=2, RiskLevel=RiskLevel.High, RiskScore=78, GeneratedAt=new DateTime(2023,6,16), Summary="Critical: Ownership gap detected 2005–2012. EC owner does not match SLR owner. Court dispute pending." },
            new RiskReport { Id=3, LandRecordId=3, RiskLevel=RiskLevel.Medium, RiskScore=44, GeneratedAt=new DateTime(2022,3,21), Summary="Area mismatch between SLR (3.90 Acres) and Passbook (3.75 Acres). Recommend field verification." }
        );

        m.Entity<RiskFlag>().HasData(
            new RiskFlag { Id=1, RiskReportId=2, FlagType=FlagType.MissingOwnershipLink, Description="Ownership chain gap: No record found for period 2005–2012", Severity=Severity.Critical },
            new RiskFlag { Id=2, RiskReportId=2, FlagType=FlagType.OwnerNameMismatch, Description="EC owner 'Ramaiah' does not match SLR owner 'UNKNOWN'", Severity=Severity.High },
            new RiskFlag { Id=3, RiskReportId=2, FlagType=FlagType.CourtDisputePending, Description="Court case No. 2018/CVL/441 pending in District Court", Severity=Severity.High },
            new RiskFlag { Id=4, RiskReportId=3, FlagType=FlagType.AreaMismatch, Description="SLR shows 3.90 Acres; Passbook shows 3.75 Acres — discrepancy of 0.15 Acres", Severity=Severity.Medium }
        );
    }
}
