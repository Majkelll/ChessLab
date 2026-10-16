namespace ChessLab.Web.Client.Services;

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
