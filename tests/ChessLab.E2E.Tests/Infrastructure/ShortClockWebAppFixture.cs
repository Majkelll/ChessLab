namespace ChessLab.E2E.Tests.Infrastructure;

public sealed class ShortClockWebAppFixture : WebAppFixture
{
    protected override double InitialClockSeconds => 2;
    protected override double ClockIncrementSeconds => 0;
}
