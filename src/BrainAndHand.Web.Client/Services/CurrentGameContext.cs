namespace BrainAndHand.Web.Client.Services;

/// <summary>Tracks which game (if any) the player currently has open, so the header breadcrumb can
/// read "~/majkel-games$ Hand and Brain" / "~/majkel-games$ Card Chess" instead of a static label —
/// or just the bare prompt on the home page, before a game's been picked.</summary>
public sealed class CurrentGameContext
{
    public string? GameName { get; private set; }

    public event Action? Changed;

    public void Set(string? name)
    {
        if (GameName == name)
            return;

        GameName = name;
        Changed?.Invoke();
    }
}
