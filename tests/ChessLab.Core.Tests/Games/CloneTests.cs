using ChessLab.Core.Bidding;
using ChessLab.Core.Chess;
using ChessLab.Core.Draft;
using ChessLab.Core.Games;
using ChessLab.Core.HandBrain;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Games;

public class CloneTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<GameKind> AllKinds => [.. Enum.GetValues<GameKind>()];

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Clone_ReportsTheSameActionsAndPosition(GameKind kind)
    {
        var original = PlayedGame(kind, plies: 12);

        var clone = CloneOf(original);

        Assert.Equal(Snapshot(original), Snapshot(clone));
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void ChangingTheClone_LeavesTheOriginalUnchanged(GameKind kind)
    {
        var original = PlayedGame(kind, plies: 12);
        var before = Snapshot(original);
        var clone = CloneOf(original);

        PlayPlies(clone, new Random(5), 8);

        Assert.Equal(before, Snapshot(original));
        Assert.NotEqual(before, Snapshot(clone));
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void CloneAndOriginal_StayEqualUnderTheSameActions(GameKind kind)
    {
        var original = PlayedGame(kind, plies: 12);
        var clone = CloneOf(original);

        PlayPlies(clone, new Random(5), 8);
        PlayPlies(original, new Random(5), 8);

        Assert.Equal(Snapshot(original), Snapshot(clone));
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void SessionClone_KeepsTheOriginalSessionUnchanged(GameKind kind)
    {
        var session = StartedSession(kind);
        PlayPlies(session.Game!, new Random(3), 12);
        var before = System.Text.Json.JsonSerializer.Serialize(session.ToStateDto(null));
        var clone = session.Clone();

        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(clone.ToStateDto(null)));

        PlayPlies(clone.Game!, new Random(5), 8);

        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(session.ToStateDto(null)));
    }

    private static IRoomSession StartedSession(GameKind kind)
    {
        var room = new Room("CLONE", Guid.NewGuid(), kind);
        foreach (var seat in room.SeatIds)
            room.ClaimSeat(seat, Guid.NewGuid(), seat.ToString());

        var session = RoomSessions.For(room);
        session.Start(TimeSpan.FromHours(1), TimeSpan.Zero, Start, 11);
        return session;
    }

    private static IGameEngineState PlayedGame(GameKind kind, int plies)
    {
        var game = StartedSession(kind).Game!;
        PlayPlies(game, new Random(3), plies);
        return game;
    }

    private static IGameEngineState CloneOf(IGameEngineState game) => game switch
    {
        Core.HandBrain.GameState state => state.Clone(),
        Core.CardChess.GameState state => state.Clone(),
        Core.ArcaneChess.GameState state => state.Clone(),
        Core.Bidding.GameState state => state.Clone(),
        Core.Progressive.GameState state => state.Clone(),
        Core.Alice.GameState state => state.Clone(),
        Core.Absorption.GameState state => state.Clone(),
        Core.Martian.GameState state => state.Clone(),
        Core.Draft.GameState state => state.Clone(),
        _ => throw new ArgumentOutOfRangeException(nameof(game)),
    };

    private static void PlayPlies(IGameEngineState game, Random rng, int plies)
    {
        for (var ply = 0; ply < plies && !game.IsGameOver; ply++)
            PlayPly(game, rng);
    }

    private static void PlayPly(IGameEngineState game, Random rng)
    {
        switch (game)
        {
            case Core.HandBrain.GameState state when state.Phase == TurnPhase.BrainSelecting:
                state.SelectPieceKind(PickFrom(state.AvailablePieceKinds(), rng));
                break;
            case Core.HandBrain.GameState state:
                Play(state.AvailableMoves(), rng, state.MakeMove);
                break;
            case Core.CardChess.GameState state:
                Play(state.AvailableMoves, rng, state.MakeMove);
                break;
            case Core.ArcaneChess.GameState state:
                Play(state.AvailableMoves, rng, state.MakeMove);
                break;
            case Core.Bidding.GameState state when state.Phase == BiddingPhase.Bidding:
                foreach (var side in (Side[])[Side.White, Side.Black])
                {
                    if (!state.HasBid(side))
                        state.SubmitBid(side, rng.Next(state.ChipsOf(side) + 1), TimeSpan.Zero);
                }

                break;
            case Core.Bidding.GameState state:
                Play(state.AvailableMoves, rng, state.MakeMove);
                break;
            case Core.Progressive.GameState state:
                Play(state.AvailableMoves, rng, state.MakeMove);
                break;
            case Core.Alice.GameState state:
                Play(state.AvailableMoves, rng, state.MakeMove);
                break;
            case Core.Absorption.GameState state:
                Play(state.AvailableMoves, rng, state.MakeMove);
                break;
            case Core.Martian.GameState state:
                Play(state.AvailableMoves, rng, state.MakeMove);
                break;
            case Core.Draft.GameState state:
                PlayDraftPly(state, rng);
                break;
        }
    }

    private static void PlayDraftPly(Core.Draft.GameState state, Random rng)
    {
        switch (state.Phase)
        {
            case DraftPhase.Drafting:
                var side = state.SideToPick!.Value;
                var pickable = Enum.GetValues<PieceKind>().Where(kind => state.CanPick(side, kind)).ToArray();
                if (pickable.Length == 0)
                    state.Pass(side);
                else
                    state.Pick(side, PickFrom(pickable, rng));
                break;
            case DraftPhase.Placing:
                foreach (var placer in (Side[])[Side.White, Side.Black])
                {
                    var kind = Enum.GetValues<PieceKind>().FirstOrDefault(k => state.Remaining(placer, k) > 0);
                    if (state.Remaining(placer, kind) == 0)
                        continue;

                    state.Place(placer, kind, PickFrom(state.SquaresFor(placer, kind), rng));
                    return;
                }

                break;
            default:
                Play(state.AvailableMoves, rng, state.MakeMove);
                break;
        }
    }

    private static void Play(IReadOnlyList<ChessMove> moves, Random rng,
        Func<Square, Square, PieceKind?, TimeSpan, ChessMove> makeMove)
    {
        var move = PickFrom(moves, rng);
        makeMove(move.From, move.To, move.PromoteTo, TimeSpan.FromSeconds(1));
    }

    private static T PickFrom<T>(IReadOnlyList<T> options, Random rng) => options[rng.Next(options.Count)];

    private static string Snapshot(IGameEngineState game)
    {
        var common = string.Join(" ; ",
            game.PositionText,
            game.SideToMove,
            game.EndResult?.ToString() ?? "-",
            string.Join(' ', game.MoveNotations),
            game.Clock.WhiteRemaining,
            game.Clock.BlackRemaining);

        return $"{common} ; {ModeSnapshot(game)}";
    }

    private static string ModeSnapshot(IGameEngineState game) => game switch
    {
        Core.HandBrain.GameState state => state.Phase == TurnPhase.BrainSelecting
            ? $"{state.Phase} {Joined(state.AvailablePieceKinds())}"
            : $"{state.Phase} {state.SelectedPieceKind} {Joined(state.AvailableMoves())}",
        Core.CardChess.GameState state =>
            $"{Joined(state.AvailableMoves)} {Joined(state.HandOf(Side.White))} {Joined(state.HandOf(Side.Black))} " +
            $"{state.HpOf(Side.White)} {state.HpOf(Side.Black)}",
        Core.ArcaneChess.GameState state =>
            $"{Joined(state.AvailableMoves)} {Joined(state.HandOf(Side.White))} {Joined(state.HandOf(Side.Black))} " +
            $"{Joined(state.SpellHandOf(Side.White))} {Joined(state.SpellHandOf(Side.Black))} " +
            $"{state.ManaOf(Side.White)} {state.ManaOf(Side.Black)} {Joined(state.ActiveEffects)}",
        Core.Bidding.GameState state =>
            $"{state.Phase} {Joined(state.AvailableMoves)} {state.ChipsOf(Side.White)} {state.ChipsOf(Side.Black)} " +
            $"{state.MarkerHolder} {state.HasBid(Side.White)} {state.HasBid(Side.Black)}",
        Core.Progressive.GameState state =>
            $"{Joined(state.AvailableMoves)} {state.SeriesNumber} {state.MovesPlayedInSeries}",
        Core.Alice.GameState state => Joined(state.AvailableMoves),
        Core.Absorption.GameState state => Joined(state.AvailableMoves),
        Core.Martian.GameState state => Joined(state.AvailableMoves),
        Core.Draft.GameState state =>
            $"{state.Phase} {state.SideToPick} {Joined(state.AvailableMoves)} {Joined(state.Pool)} " +
            $"{Joined(state.PicksOf(Side.White))} {Joined(state.PicksOf(Side.Black))} " +
            $"{Joined(state.PlacementsOf(Side.White))} {Joined(state.PlacementsOf(Side.Black))}",
        _ => throw new ArgumentOutOfRangeException(nameof(game)),
    };

    private static string Joined<T>(IEnumerable<T> items) => string.Join(',', items);
}
