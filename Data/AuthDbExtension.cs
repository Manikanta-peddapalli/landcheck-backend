// ============================================================
//  Addition to LandCheckDbContext — Auth tables
//  File: Data/AuthDbExtension.cs
// ============================================================

using LandCheck.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LandCheck.API.Data;

// Add these DbSets + config to LandCheckDbContext:
//
//   public DbSet<AppUser>      Users         { get; set; }
//   public DbSet<RefreshToken> RefreshTokens { get; set; }
//
// And call ConfigureAuthEntities(modelBuilder) inside OnModelCreating.

public static class AuthDbExtension
{
    public static void ConfigureAuthEntities(ModelBuilder m)
    {
        // ── AppUser ────────────────────────────────────────
        m.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.PasswordHash).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();

            e.HasMany(x => x.RefreshTokens)
             .WithOne(x => x.AppUser)
             .HasForeignKey(x => x.AppUserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── RefreshToken ───────────────────────────────────
        m.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Token).IsRequired().HasMaxLength(256);
        });

        // ── Seed Users (demo accounts) ─────────────────────
        m.Entity<AppUser>().HasData(
            new AppUser
            {
                Id           = 1,
                FullName     = "Ravi Kumar (Demo Farmer)",
                Email        = "farmer@demo.com",
                Phone        = "9876543210",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo@1234"),
                Role         = UserRole.Farmer,
                IsActive     = true,
                IsVerified   = true,
                CreatedAt    = new DateTime(2024, 1, 1),
                LastLoginAt  = new DateTime(2024, 1, 1)
            },
            new AppUser
            {
                Id           = 2,
                FullName     = "Andhra Pradesh Grameena Bank",
                Email        = "bank@demo.com",
                Phone        = "9876500001",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo@1234"),
                Role         = UserRole.Bank,
                Organisation = "AP Grameena Bank",
                IsActive     = true,
                IsVerified   = true,
                CreatedAt    = new DateTime(2024, 1, 1),
                LastLoginAt  = new DateTime(2024, 1, 1)
            },
            new AppUser
            {
                Id           = 3,
                FullName     = "Advocate Srinivas Rao",
                Email        = "lawyer@demo.com",
                Phone        = "9876500002",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo@1234"),
                Role         = UserRole.Lawyer,
                Organisation = "Rao & Associates, Vijayawada",
                IsActive     = true,
                IsVerified   = true,
                CreatedAt    = new DateTime(2024, 1, 1),
                LastLoginAt  = new DateTime(2024, 1, 1)
            },
            new AppUser
            {
                Id           = 4,
                FullName     = "LandCheck Admin",
                Email        = "admin@landcheck.in",
                Phone        = "9876500000",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@2024!"),
                Role         = UserRole.Government,
                Organisation = "LandCheck Platform",
                IsActive     = true,
                IsVerified   = true,
                CreatedAt    = new DateTime(2024, 1, 1),
                LastLoginAt  = new DateTime(2024, 1, 1)
            }
        );
    }
}
