using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Contracts;

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
    ProgressiveSectionDto? Progressive = null,
    AliceSectionDto? Alice = null,
    AbsorptionSectionDto? Absorption = null,
    MartianSectionDto? Martian = null,
    DraftSectionDto? Draft = null);

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
    ProgressiveSectionDto? Progressive = null,
    AliceSectionDto? Alice = null,
    AbsorptionSectionDto? Absorption = null,
    MartianSectionDto? Martian = null,
    DraftSectionDto? Draft = null);
