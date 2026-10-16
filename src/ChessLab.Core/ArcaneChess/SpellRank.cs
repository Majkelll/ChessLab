namespace ChessLab.Core.ArcaneChess;

public enum SpellRank
{
    Shield,
    FreezeSquare,
    Peek,
    SnapSwap,
    Feint,
    Jam,
    DeepBreath,

    Swap,
    Teleport,
    PinDown,
    Disarm,
    Mend,
    Dispel,
    Reshuffle,

    ExtraTurn,
    Execution,
    Restoration,
    MindSwap,
    TimeFreeze,
}

public static class SpellRankExtensions
{
    public static IReadOnlyList<SpellRank> All { get; } = Enum.GetValues<SpellRank>();

    public static int Cost(this SpellRank spell) => spell switch
    {
        SpellRank.Shield or SpellRank.FreezeSquare or SpellRank.Peek or SpellRank.SnapSwap
            or SpellRank.Feint or SpellRank.Jam or SpellRank.DeepBreath => 1,
        SpellRank.Swap or SpellRank.Teleport or SpellRank.PinDown or SpellRank.Disarm
            or SpellRank.Mend or SpellRank.Dispel or SpellRank.Reshuffle => 2,
        SpellRank.ExtraTurn or SpellRank.Execution or SpellRank.Restoration
            or SpellRank.MindSwap or SpellRank.TimeFreeze => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(spell)),
    };

    public static string Label(this SpellRank spell) => spell switch
    {
        SpellRank.Shield => "Shield",
        SpellRank.FreezeSquare => "Freeze Square",
        SpellRank.Peek => "Peek",
        SpellRank.SnapSwap => "Snap Swap",
        SpellRank.Feint => "Feint",
        SpellRank.Jam => "Jam",
        SpellRank.DeepBreath => "Deep Breath",
        SpellRank.Swap => "Swap",
        SpellRank.Teleport => "Teleport",
        SpellRank.PinDown => "Pin Down",
        SpellRank.Disarm => "Disarm",
        SpellRank.Mend => "Mend",
        SpellRank.Dispel => "Dispel",
        SpellRank.Reshuffle => "Reshuffle",
        SpellRank.ExtraTurn => "Extra Turn",
        SpellRank.Execution => "Execution",
        SpellRank.Restoration => "Restoration",
        SpellRank.MindSwap => "Mind Swap",
        SpellRank.TimeFreeze => "Time Freeze",
        _ => throw new ArgumentOutOfRangeException(nameof(spell)),
    };

    public static string Description(this SpellRank spell) => spell switch
    {
        SpellRank.Shield => "Target one of your own pieces — it can't be captured on your opponent's next turn.",
        SpellRank.FreezeSquare => "Target an empty square — no opposing piece can move onto it on their next turn.",
        SpellRank.Peek => "See your opponent's rank-card hand until your next turn begins.",
        SpellRank.SnapSwap => "Immediately discard and redraw one rank card from your own hand — no delay.",
        SpellRank.Feint => "Cancel your opponent's currently marked reroll selection.",
        SpellRank.Jam => "Your opponent gains no mana at the start of their next turn.",
        SpellRank.DeepBreath => "If your move this turn is an Emergency Move, it costs 0 HP instead of 1.",
        SpellRank.Swap => "Swap two of your own pieces (not the king) — illegal if it leaves your king in check.",
        SpellRank.Teleport => "Move one of your own pieces (not the king) to any empty square — a pawn can't land on the back rank; illegal if it leaves your king in check.",
        SpellRank.PinDown => "Target an opposing piece (not their king) — it can't move at all on their next turn.",
        SpellRank.Disarm => "Target an opposing piece (not their king) — it can move but not capture on their next turn.",
        SpellRank.Mend => "Restore 1 HP, up to your starting maximum.",
        SpellRank.Dispel => "Remove any one currently active Shield, Freeze, Pin Down, or Disarm effect at a target square.",
        SpellRank.Reshuffle => "Your opponent discards one random rank card and immediately draws a replacement.",
        SpellRank.ExtraTurn => "After your move this turn resolves, take one more full turn immediately (you can't cast another spell during it).",
        SpellRank.Execution => "Remove one opposing Pawn from the board directly — never the King, Queen, Rook, Bishop, or Knight.",
        SpellRank.Restoration => "Restore 2 HP, up to your starting maximum.",
        SpellRank.MindSwap => "Swap your King with one of your own other pieces — illegal if it leaves your king in check.",
        SpellRank.TimeFreeze => "Target two empty squares — no opposing piece can move onto either one on their next turn.",
        _ => throw new ArgumentOutOfRangeException(nameof(spell)),
    };
}
