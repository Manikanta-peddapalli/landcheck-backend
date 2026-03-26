// ============================================================
//  LandCheck – Auth DB Extension (FIXED - No Seed Data)
//  File: Data/AuthDbExtension.cs
// ============================================================

using LandCheck.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LandCheck.API.Data;

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

        // NOTE: No seed data here — caused BCrypt crash in PostgreSQL
        // Demo accounts are created via /api/auth/register endpoint
    }
}
