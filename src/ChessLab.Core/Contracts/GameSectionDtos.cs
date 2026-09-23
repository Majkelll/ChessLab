using ChessLab.Core.ArcaneChess;
using ChessLab.Core.Bidding;
using ChessLab.Core.Draft;
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

public sealed record AliceSectionDto(string BoardAFen, string BoardBFen, bool IsInCheck);

public sealed record AbsorptionSectionDto(string Fen, string PowersText, bool IsInCheck);

public sealed record MartianPyramidDto(Square Square, int Value);

public sealed record MartianSectionDto(
    IReadOnlyList<MartianPyramidDto> Pyramids,
    int WhiteScore,
    int BlackScore);

public sealed record DraftPoolEntryDto(PieceKind Kind, int Remaining, int Cost);

public sealed record DraftPlacementDto(Square Square, PieceKind Kind);

public sealed record DraftPlaceableDto(PieceKind Kind, IReadOnlyList<Square> Squares);

public sealed record DraftSectionDto(
    DraftPhase Phase,
    Side? SideToPick,
    IReadOnlyList<DraftPoolEntryDto> Pool,
    IReadOnlyList<PieceKind> WhitePicks,
    IReadOnlyList<PieceKind> BlackPicks,
    int WhiteBudgetLeft,
    int BlackBudgetLeft,
    bool WhitePassed,
    bool BlackPassed,
    IReadOnlyList<DraftPlacementDto> WhitePlacements,
    IReadOnlyList<DraftPlacementDto> BlackPlacements,
    bool WhiteReady,
    bool BlackReady,
    int WhiteTimeBonusSeconds,
    int BlackTimeBonusSeconds,
    IReadOnlyList<DraftPlaceableDto> WhitePlaceable,
    IReadOnlyList<DraftPlaceableDto> BlackPlaceable);
