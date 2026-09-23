using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Contracts;

/// <summary>
/// One shape for every game kind: the fields all of them have, plus at most one filled-in section
/// per set of mode-specific rules. Arcane Chess fills both <see cref="CardChess"/> and
/// <see cref="Arcane"/>, since it's Card Chess with spells on top. Sections are nullable concrete
/// records rather than a polymorphic hierarchy because the hub speaks MessagePack.
/// </summary>
public sealed record GameStateEnvelopeDto(
    GameKind Kind,
    Side SideToMove,
    string PositionText,
    IReadOnlyList<ChessMove> AvailableMoves,
    IReadOnlyList<ChessMove> MoveHistory,
    long WhiteRemainingMs,
    long BlackRemainingMs,
    bool IsGameOver,
    GameEndReason? EndReason,
    Side? Winner,
    HandBrainSectionDto? HandBrain = null,
    CardChessSectionDto? CardChess = null,
    ArcaneChessSectionDto? Arcane = null,
    BiddingSectionDto? Bidding = null,
    ProgressiveSectionDto? Progressive = null);

/// <summary>The same thing as <see cref="GameStateEnvelopeDto"/>, but carrying only the latest move
/// and the resulting history length, so a client that's been following along doesn't have the whole
/// move list pushed to it every turn.</summary>
public sealed record GameUpdateEnvelopeDto(
    GameKind Kind,
    Side SideToMove,
    string PositionText,
    IReadOnlyList<ChessMove> AvailableMoves,
    ChessMove? LatestMove,
    int MoveHistoryCount,
    long WhiteRemainingMs,
    long BlackRemainingMs,
    bool IsGameOver,
    GameEndReason? EndReason,
    Side? Winner,
    HandBrainSectionDto? HandBrain = null,
    CardChessSectionDto? CardChess = null,
    ArcaneChessSectionDto? Arcane = null,
    BiddingSectionDto? Bidding = null,
    ProgressiveSectionDto? Progressive = null);
