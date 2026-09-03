using ChessLab.Core.ArcaneChess;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;

namespace ChessLab.Core.Contracts;

public sealed record ArcaneChessUpdateDto(
    Side SideToMove,
    IReadOnlyList<CardRank> WhiteHand,
    IReadOnlyList<CardRank> BlackHand,
    IReadOnlyList<CardRank> WhitePendingReroll,
    IReadOnlyList<CardRank> BlackPendingReroll,
    bool EmergencyMoveAvailable,
    bool HandHasNoPlayableCard,
    IReadOnlyList<ChessMove> AvailableMoves,
    ChessMove? LatestMove,
    int MoveHistoryCount,
    string Fen,
    int WhiteHp,
    int BlackHp,
    long WhiteRemainingMs,
    long BlackRemainingMs,
    bool IsGameOver,
    GameEndReason? EndReason,
    Side? Winner,
    IReadOnlyList<SpellRank> WhiteSpellHand,
    IReadOnlyList<SpellRank> BlackSpellHand,
    int WhiteMana,
    int BlackMana,
    bool WhiteSeesBlackHand,
    bool BlackSeesWhiteHand,
    IReadOnlyList<ArcaneEffect> ActiveEffects);
