using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;

namespace ChessLab.Core.ArcaneChess;

/// <summary>What a spell is being cast at — only the fields a given <see cref="SpellRank"/> needs
/// are read; the rest are ignored.</summary>
public readonly record struct SpellTarget(Square? Primary = null, Square? Secondary = null, CardRank? HandCard = null);
