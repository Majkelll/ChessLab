using System.Text.Json;
using ChessLab.Core.Chess;

namespace ChessLab.Core.Tests.Chess;

public class SquareJsonTests
{
    [Theory]
    [InlineData("a1")]
    [InlineData("e2")]
    [InlineData("h8")]
    [InlineData("d4")]
    public void RoundTrips_through_the_same_json_serializer_SignalR_uses_for_hub_messages(string algebraic)
    {
        var square = Square.Parse(algebraic);

        var json = JsonSerializer.Serialize(square);
        var roundTripped = JsonSerializer.Deserialize<Square>(json);

        Assert.Equal(square, roundTripped);
        Assert.Equal(algebraic, roundTripped.ToString());
    }

    [Fact]
    public void ChessMove_From_and_To_round_trip_correctly_as_part_of_a_larger_object()
    {
        var move = new ChessMove(Square.Parse("e2"), Square.Parse("e4"), PieceKind.Pawn, null, null, false, false, "e4");

        var json = JsonSerializer.Serialize(move);
        var roundTripped = JsonSerializer.Deserialize<ChessMove>(json);

        Assert.Equal(Square.Parse("e2"), roundTripped.From);
        Assert.Equal(Square.Parse("e4"), roundTripped.To);
    }
}
