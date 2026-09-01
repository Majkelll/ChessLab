using Microsoft.EntityFrameworkCore;

namespace ChessLab.Data;

public class ChessLabDbContext(DbContextOptions<ChessLabDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(u => u.GoogleSubjectId).IsUnique();
        });
    }
}
