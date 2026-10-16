using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Engines;

public static class EngineRegistry
{
    public static IEngineUnderTest For(GameKind kind) => new CoreReferenceEngine(kind);
}
