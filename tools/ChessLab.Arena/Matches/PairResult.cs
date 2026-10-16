using ChessLab.Core.Chess;

namespace ChessLab.Arena.Matches;

public sealed record PairResult(int Index, string Opening, int Seed, GameRecord CandidateWhite, GameRecord CandidateBlack)
{
    public double CandidatePoints => CandidateWhite.PointsFor(Side.White) + CandidateBlack.PointsFor(Side.Black);

    public int PentanomialIndex => (int)Math.Round(CandidatePoints * 2);
}
