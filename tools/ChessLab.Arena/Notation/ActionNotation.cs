using ChessLab.Core.ArcaneChess;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Notation;

public static class ActionNotation
{
    public static string Format(SeatAction seatAction) => Format(seatAction.Seat.Side, seatAction.Action);

    public static string Format(Side side, GameAction action) => $"{SideLetter(side)}:{Body(action)}";

    public static SeatAction Parse(string token, GameKind kind)
    {
        var separator = token.IndexOf(':');
        if (separator != 1)
            throw new FormatException($"'{token}' is not an action: expected w: or b: followed by the action.");

        var side = token[0] switch
        {
            'w' => Side.White,
            'b' => Side.Black,
            _ => throw new FormatException($"'{token}' does not start with w: or b:."),
        };

        var action = ParseBody(token[2..], token);
        return new SeatAction(new SeatId(side, RoleOf(kind, action)), action);
    }

    private static SeatRole RoleOf(GameKind kind, GameAction action) =>
        kind != GameKind.HandAndBrain ? SeatRole.Player
        : action.Kind == GameActionKind.SelectPieceKind ? SeatRole.Brain
        : SeatRole.Hand;

    private static string Body(GameAction action) => action.Kind switch
    {
        GameActionKind.Move => $"{action.From}{action.To}{PromotionLetter(action.PromoteTo)}",
        GameActionKind.SelectPieceKind => $"select={PieceLetter(action.SelectedKind!.Value)}",
        GameActionKind.SelectReroll => $"reroll={string.Join('+', action.Cards ?? [])}",
        GameActionKind.CastSpell => CastBody(action),
        GameActionKind.SubmitBid => $"bid={action.Amount}",
        GameActionKind.DraftPick => $"pick={PieceLetter(action.SelectedKind!.Value)}",
        GameActionKind.DraftPass => "pass",
        GameActionKind.PlacePiece => $"place={PieceLetter(action.SelectedKind!.Value)}@{action.To}",
        GameActionKind.UnplacePiece => $"unplace={action.To}",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unknown action kind."),
    };

    private static string CastBody(GameAction action)
    {
        var target = action.Target ?? default;
        var body = $"cast={action.Spell}";
        if (target.Primary is { } primary)
            body += $"@{primary}";
        if (target.Secondary is { } secondary)
            body += $"+{secondary}";
        if (target.HandCard is { } card)
            body += $"#{card}";

        return body;
    }

    private static GameAction ParseBody(string body, string token)
    {
        try
        {
            return body switch
            {
                "pass" => GameAction.DraftPass(),
                _ when body.StartsWith("select=", StringComparison.Ordinal) =>
                    GameAction.SelectPieceKind(ParsePiece(body["select=".Length..])),
                _ when body.StartsWith("reroll=", StringComparison.Ordinal) =>
                    GameAction.SelectReroll(ParseCards(body["reroll=".Length..])),
                _ when body.StartsWith("cast=", StringComparison.Ordinal) => ParseCast(body["cast=".Length..]),
                _ when body.StartsWith("bid=", StringComparison.Ordinal) =>
                    GameAction.SubmitBid(int.Parse(body["bid=".Length..])),
                _ when body.StartsWith("pick=", StringComparison.Ordinal) =>
                    GameAction.DraftPick(ParsePiece(body["pick=".Length..])),
                _ when body.StartsWith("place=", StringComparison.Ordinal) => ParsePlace(body["place=".Length..]),
                _ when body.StartsWith("unplace=", StringComparison.Ordinal) =>
                    GameAction.UnplacePiece(Square.Parse(body["unplace=".Length..])),
                _ => ParseMove(body),
            };
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new FormatException($"'{token}' is not a valid action: {ex.Message}", ex);
        }
    }

    private static GameAction ParseMove(string body)
    {
        if (body.Length is not (4 or 5))
            throw new FormatException("a move is written as from and to squares, like e2e4 or e7e8q");

        var promotion = body.Length == 5 ? ParsePiece(body[4..]) : (PieceKind?)null;
        return GameAction.MovePiece(Square.Parse(body[..2]), Square.Parse(body[2..4]), promotion);
    }

    private static GameAction ParsePlace(string body)
    {
        var parts = body.Split('@');
        if (parts.Length != 2)
            throw new FormatException("a placement is written as place=k@e1");

        return GameAction.PlacePiece(ParsePiece(parts[0]), Square.Parse(parts[1]));
    }

    private static GameAction ParseCast(string body)
    {
        CardRank? card = null;
        var hash = body.IndexOf('#');
        if (hash >= 0)
        {
            card = Enum.Parse<CardRank>(body[(hash + 1)..]);
            body = body[..hash];
        }

        Square? primary = null;
        Square? secondary = null;
        var at = body.IndexOf('@');
        if (at >= 0)
        {
            var squares = body[(at + 1)..].Split('+');
            primary = Square.Parse(squares[0]);
            if (squares.Length > 1)
                secondary = Square.Parse(squares[1]);
            body = body[..at];
        }

        return GameAction.CastSpell(Enum.Parse<SpellRank>(body), new SpellTarget(primary, secondary, card));
    }

    private static IReadOnlyList<CardRank> ParseCards(string body) =>
        body.Length == 0 ? [] : [.. body.Split('+').Select(Enum.Parse<CardRank>)];

    private static char SideLetter(Side side) => side == Side.White ? 'w' : 'b';

    private static string PromotionLetter(PieceKind? kind) => kind is { } piece ? PieceLetter(piece).ToString() : "";

    public static char PieceLetter(PieceKind kind) => kind switch
    {
        PieceKind.Pawn => 'p',
        PieceKind.Knight => 'n',
        PieceKind.Bishop => 'b',
        PieceKind.Rook => 'r',
        PieceKind.Queen => 'q',
        PieceKind.King => 'k',
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static PieceKind ParsePiece(string letter) => letter switch
    {
        "p" => PieceKind.Pawn,
        "n" => PieceKind.Knight,
        "b" => PieceKind.Bishop,
        "r" => PieceKind.Rook,
        "q" => PieceKind.Queen,
        "k" => PieceKind.King,
        _ => throw new FormatException($"'{letter}' is not a piece letter (p, n, b, r, q, k)"),
    };
}
