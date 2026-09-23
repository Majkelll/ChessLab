using ChessLab.Core.ArcaneChess;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;

namespace ChessLab.Core.Games;

public enum GameActionKind
{
    Move,
    SelectPieceKind,
    SelectReroll,
    CastSpell,
}

/// <summary>
/// Everything a seated player can do on their turn, in one shape the hub can carry for any game
/// kind. Only the fields the <see cref="Kind"/> calls for are read; a session rejects an action
/// whose kind its rules don't have.
/// </summary>
public sealed record GameAction(
    GameActionKind Kind,
    Square? From = null,
    Square? To = null,
    PieceKind? PromoteTo = null,
    PieceKind? SelectedKind = null,
    IReadOnlyList<CardRank>? Cards = null,
    SpellRank? Spell = null,
    SpellTarget? Target = null)
{
    public static GameAction MovePiece(Square from, Square to, PieceKind? promoteTo = null) =>
        new(GameActionKind.Move, From: from, To: to, PromoteTo: promoteTo);

    public static GameAction SelectPieceKind(PieceKind kind) =>
        new(GameActionKind.SelectPieceKind, SelectedKind: kind);

    public static GameAction SelectReroll(IReadOnlyList<CardRank> cards) =>
        new(GameActionKind.SelectReroll, Cards: cards);

    public static GameAction CastSpell(SpellRank spell, SpellTarget target) =>
        new(GameActionKind.CastSpell, Spell: spell, Target: target);
}
