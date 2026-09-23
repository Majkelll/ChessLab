namespace ChessLab.E2E.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<WebAppFixture>, ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "ChessLab app";
}

[CollectionDefinition(Name)]
public sealed class ShortClockAppCollection : ICollectionFixture<ShortClockWebAppFixture>, ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "ChessLab app (short clock)";
}

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
