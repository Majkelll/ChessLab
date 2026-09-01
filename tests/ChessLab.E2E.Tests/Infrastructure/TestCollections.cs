namespace ChessLab.E2E.Tests.Infrastructure;

/// <summary>Shared app instance (default clock) + shared browser — used by most test classes.</summary>
[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<WebAppFixture>, ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "ChessLab app";
}

/// <summary>A dedicated app instance with a near-expired clock, for testing the timeout path.</summary>
[CollectionDefinition(Name)]
public sealed class ShortClockAppCollection : ICollectionFixture<ShortClockWebAppFixture>, ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "ChessLab app (short clock)";
}
