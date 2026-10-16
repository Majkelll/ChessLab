using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;

namespace ChessLab.Core.ArcaneChess;

public readonly record struct SpellTarget(Square? Primary = null, Square? Secondary = null, CardRank? HandCard = null);
