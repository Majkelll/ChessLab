namespace BrainAndHand.E2E.Tests.Infrastructure;

/// <summary>A game clock that starts already almost expired, so the timeout path is reachable in seconds.</summary>
public sealed class ShortClockWebAppFixture : WebAppFixture
{
    protected override double InitialClockSeconds => 2;
    protected override double ClockIncrementSeconds => 0;
}
