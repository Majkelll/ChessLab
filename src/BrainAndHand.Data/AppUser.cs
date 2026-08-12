namespace BrainAndHand.Data;

public class AppUser
{
    public Guid Id { get; set; }
    public required string GoogleSubjectId { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastLoginAt { get; set; }
}
