using Microsoft.EntityFrameworkCore;

namespace BrainAndHand.Data;

public class BrainAndHandDbContext(DbContextOptions<BrainAndHandDbContext> options) : DbContext(options)
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
