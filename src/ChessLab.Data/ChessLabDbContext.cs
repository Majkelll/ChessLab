using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChessLab.Data;

public class ChessLabDbContext(DbContextOptions<ChessLabDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<GameRecord> GameRecords => Set<GameRecord>();

    public DbSet<GameRecordPlayer> GameRecordPlayers => Set<GameRecordPlayer>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(u => u.GoogleSubjectId).IsUnique();
        });

        modelBuilder.Entity<GameRecord>(entity =>
        {
            entity.HasIndex(g => g.RoomCode).IsUnique();
            entity.HasIndex(g => g.FinishedAtUtc);
            entity.Property(g => g.RoomCode).HasMaxLength(16);
            entity.Property(g => g.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(g => g.EndReason).HasConversion<string>().HasMaxLength(32);
            entity.Property(g => g.Winner).HasConversion<string>().HasMaxLength(8);
            entity.HasMany(g => g.Players)
                .WithOne(p => p.GameRecord!)
                .HasForeignKey(p => p.GameRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameRecordPlayer>(entity =>
        {
            entity.HasIndex(p => p.UserId);
            entity.Property(p => p.Side).HasConversion<string>().HasMaxLength(8);
            entity.Property(p => p.Role).HasConversion<string>().HasMaxLength(16);
            entity.Property(p => p.BotDifficulty).HasConversion<string>().HasMaxLength(16);
            entity.Property(p => p.DisplayName).HasMaxLength(128);
        });
    }
}
