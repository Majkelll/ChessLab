namespace BrainAndHand.Core.HandBrain;

public enum TurnPhase
{
    /// <summary>Brain must announce a piece kind that has at least one legal move.</summary>
    BrainSelecting,

    /// <summary>Hand must move one of the announced piece kind's pieces.</summary>
    HandMoving,
}
