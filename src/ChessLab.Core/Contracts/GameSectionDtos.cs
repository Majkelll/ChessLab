using ChessLab.Core.ArcaneChess;
using ChessLab.Core.Bidding;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.HandBrain;

namespace ChessLab.Core.Contracts;

public sealed record HandBrainSectionDto(
    TurnPhase Phase,
    PieceKind? SelectedPieceKind,
    IReadOnlyList<PieceKind> AvailablePieceKinds);

public sealed record CardChessSectionDto(
    IReadOnlyList<CardRank> WhiteHand,
    IReadOnlyList<CardRank> BlackHand,
    IReadOnlyList<CardRank> WhitePendingReroll,
    IReadOnlyList<CardRank> BlackPendingReroll,
    bool EmergencyMoveAvailable,
    bool HandHasNoPlayableCard,
    int WhiteHp,
    int BlackHp);

public sealed record ArcaneChessSectionDto(
    IReadOnlyList<SpellRank> WhiteSpellHand,
    IReadOnlyList<SpellRank> BlackSpellHand,
    int WhiteMana,
    int BlackMana,
    bool WhiteSeesBlackHand,
    bool BlackSeesWhiteHand,
    IReadOnlyList<ArcaneEffect> ActiveEffects);

public sealed record BiddingSectionDto(
    BiddingPhase Phase,
    int WhiteChips,
    int BlackChips,
    Side MarkerHolder,
    bool WhiteHasBid,
    bool BlackHasBid,
    int? LastWhiteBid,
    int? LastBlackBid);

public sealed record ProgressiveSectionDto(int SeriesNumber, int MovesPlayedInSeries, int MovesLeftInSeries);
