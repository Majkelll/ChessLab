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

// The long play-through tests get their own collections, three of them, so the nine games run
// three at a time instead of one after another — each collection pays for its own app and browser,
// which is the trade that turns five minutes of wall time into under two.

[CollectionDefinition(Name)]
public sealed class LongGameClassicsCollection
    : ICollectionFixture<WebAppFixture>, ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "ChessLab long games (original modes)";
}

[CollectionDefinition(Name)]
public sealed class LongGameChessLikeCollection
    : ICollectionFixture<WebAppFixture>, ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "ChessLab long games (chess-like modes)";
}

[CollectionDefinition(Name)]
public sealed class LongGameOddOnesCollection
    : ICollectionFixture<WebAppFixture>, ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "ChessLab long games (unusual modes)";
}
