using Microsoft.EntityFrameworkCore;

namespace BrainAndHand.Data;

public class UserService(BrainAndHandDbContext db)
{
    public async Task<AppUser> UpsertFromGoogleLoginAsync(
        string googleSubjectId, string email, string displayName, string? avatarUrl, CancellationToken ct = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.GoogleSubjectId == googleSubjectId, ct);
        var now = DateTimeOffset.UtcNow;

        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                GoogleSubjectId = googleSubjectId,
                Email = email,
                DisplayName = displayName,
                AvatarUrl = avatarUrl,
                CreatedAt = now,
                LastLoginAt = now,
            };
            db.Users.Add(user);
        }
        else
        {
            user.Email = email;
            user.DisplayName = displayName;
            user.AvatarUrl = avatarUrl;
            user.LastLoginAt = now;
        }

        await db.SaveChangesAsync(ct);
        return user;
    }
}
