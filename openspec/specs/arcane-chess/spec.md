# arcane-chess Specification

## Purpose
Describe Arcane Chess: Card Chess with a second hand of spell cards paid for with mana. Everything in the card-chess capability applies; this capability covers only the additions.

## Requirements

### Requirement: Spell hands and mana
The system SHALL give each side its own shuffled deck of the nineteen spells and a hand of three spell cards. The side to move first SHALL start with 1 mana and the other side with 0. A side SHALL gain 1 mana when its turn begins, up to a maximum of 3. Spell draws SHALL NOT be filtered, so a hand can hold the same spell twice once the deck has been reshuffled.

#### Scenario: Opening state
- **WHEN** a game starts
- **THEN** each side holds three spell cards, White has 1 mana and Black has 0

#### Scenario: Mana at the cap
- **WHEN** a side with 3 mana begins its turn
- **THEN** it still has 3 mana

### Requirement: Spell costs
The system SHALL charge 1 mana for Shield, Freeze Square, Peek, Snap Swap, Feint, Jam and Deep Breath; 2 mana for Swap, Teleport, Pin Down, Disarm, Mend, Dispel and Reshuffle; and 3 mana for Extra Turn, Execution, Restoration, Mind Swap and Time Freeze.

#### Scenario: Not enough mana
- **WHEN** a side with 1 mana casts a spell that costs 2
- **THEN** the cast is rejected with a message naming the cost and the mana held

### Requirement: Casting
The system SHALL let the side to move cast at most one spell per turn, before moving, from its spell hand. A successful cast SHALL deduct the cost, discard the card and draw a replacement. A rejected cast SHALL consume neither mana nor the card. Casting SHALL NOT end the turn. A cast SHALL be checked in this order: one spell per turn, mana, spell in hand, then the spell's own target rules.

#### Scenario: Successful cast
- **WHEN** a side with 2 mana casts a 1-mana spell from its hand
- **THEN** it has 1 mana, the card is replaced and it still holds three spell cards

#### Scenario: Spell not in hand
- **WHEN** a side casts a spell it does not hold
- **THEN** the cast is rejected with `<spell> is not in hand.`

#### Scenario: Spell neither held nor affordable
- **WHEN** a side casts a spell it does not hold and cannot afford
- **THEN** the cast is rejected with the mana message

#### Scenario: Second spell in a turn
- **WHEN** a side casts a second spell before moving
- **THEN** the cast is rejected with `Already cast a spell this turn.`

#### Scenario: Invalid target
- **WHEN** Teleport would leave the caster's own king in check
- **THEN** the cast is rejected with `This would leave your own king in check.`
- **AND** mana and spell hand are unchanged

### Requirement: Restricting effects
The system SHALL apply Shield, Freeze Square, Time Freeze, Pin Down and Disarm as effects on the opponent that last for the opponent's next turn and are removed once the opponent has moved. Effects SHALL be bound to squares, not pieces, and SHALL restrict the moves the opponent's hand already allows.

#### Scenario: Shield
- **WHEN** a side casts Shield on one of its own pieces, the king included
- **THEN** the opponent cannot capture on that square during its next turn
- **AND** the restriction is gone on the turn after

#### Scenario: Freeze Square
- **WHEN** a side casts Freeze Square on an empty square
- **THEN** the opponent cannot move any piece onto that square during its next turn

#### Scenario: Time Freeze
- **WHEN** a side casts Time Freeze on two different empty squares
- **THEN** the opponent cannot move onto either square during its next turn

#### Scenario: Pin Down
- **WHEN** a side casts Pin Down on an opposing piece other than the king
- **THEN** that piece cannot move during the opponent's next turn

#### Scenario: Disarm
- **WHEN** a side casts Disarm on an opposing piece other than the king
- **THEN** that piece can move but cannot capture during the opponent's next turn

#### Scenario: Effects would leave no move
- **WHEN** the active effects would remove every available move of the affected side
- **THEN** all effects are ignored for that turn and every move the hand allows is available

#### Scenario: Wrong target
- **WHEN** Freeze Square targets an occupied square, or Pin Down targets the opposing king
- **THEN** the cast is rejected with `<square> is not empty.` or `The king can't be targeted.`

#### Scenario: Missing target
- **WHEN** a spell that needs a square is cast without one
- **THEN** the cast is rejected with `This spell needs a target square.`

### Requirement: Dispel
The system SHALL remove every active effect on the targeted square when Dispel is cast.

#### Scenario: Effect present
- **WHEN** Dispel targets a square carrying an effect
- **THEN** the effect is removed

#### Scenario: No effect present
- **WHEN** Dispel targets a square with no effect
- **THEN** the cast is rejected with `No active effect at <square>.`

### Requirement: Board-altering spells
The system SHALL change the position directly for Swap, Teleport, Mind Swap and Execution (restarting the repetition count, see chess-rules), and SHALL reject any of them after which the caster's king is in check, including when it was already in check before the cast. A change that touches a king or rook home square SHALL revoke the matching castling rights and any en passant opportunity SHALL be cleared.

#### Scenario: Swap
- **WHEN** a side casts Swap on two different pieces of its own, neither a king
- **THEN** the two pieces exchange squares

#### Scenario: Teleport
- **WHEN** a side casts Teleport on one of its own non-king pieces and an empty square
- **THEN** the piece stands on the empty square

#### Scenario: Mind Swap
- **WHEN** a side casts Mind Swap on one of its own non-king pieces
- **THEN** the king and that piece exchange squares

#### Scenario: Execution
- **WHEN** a side casts Execution on an opposing pawn
- **THEN** the pawn is removed from the board

#### Scenario: Execution on a non-pawn
- **WHEN** a side casts Execution on an opposing knight
- **THEN** the cast is rejected with `<square> is not a pawn.`

#### Scenario: Pawn sent to a back rank
- **WHEN** Swap, Teleport or Mind Swap would place a pawn on rank 1 or rank 8
- **THEN** the cast is rejected with `A pawn can't end up on the back rank.`

#### Scenario: King as Swap target
- **WHEN** Swap or Teleport targets the caster's king
- **THEN** the cast is rejected with `The king can't be targeted this way.`

#### Scenario: Caster's reroll resolves early
- **WHEN** a side with cards marked for reroll successfully casts Swap, Teleport, Mind Swap, Execution or Snap Swap
- **THEN** its marked cards are exchanged immediately instead of at its next turn

### Requirement: Hand and resource spells
The system SHALL apply Peek, Snap Swap, Feint, Jam, Reshuffle, Mend, Restoration and Deep Breath as follows.

#### Scenario: Peek
- **WHEN** a side casts Peek
- **THEN** the caster receives the opponent's rank hand and reroll marks until the caster's next turn begins

#### Scenario: Snap Swap
- **WHEN** a side casts Snap Swap naming a rank card in its own hand
- **THEN** that card is discarded and replaced immediately, preferring a different piece kind as for a reroll

#### Scenario: Snap Swap without a card
- **WHEN** a side casts Snap Swap without naming a card
- **THEN** the cast is rejected with `Snap Swap needs a hand card.`

#### Scenario: Feint
- **WHEN** a side casts Feint while the opponent has cards marked for reroll
- **THEN** the opponent's reroll selection is cleared and those cards are not exchanged

#### Scenario: Jam
- **WHEN** a side casts Jam
- **THEN** the opponent gains no mana when its next turn begins

#### Scenario: Reshuffle
- **WHEN** a side casts Reshuffle
- **THEN** one random rank card in the opponent's hand is discarded and replaced

#### Scenario: Mend
- **WHEN** a side with 3 HP casts Mend
- **THEN** it still has 3 HP

#### Scenario: Restoration
- **WHEN** a side with 1 HP casts Restoration
- **THEN** it has 3 HP

#### Scenario: Deep Breath
- **WHEN** a side casts Deep Breath and then plays an emergency move in the same turn
- **THEN** it loses no HP

#### Scenario: Spell with nothing to gain
- **WHEN** a side casts Feint while the opponent has nothing marked, or Mend or Restoration at full HP
- **THEN** the cast succeeds and the mana is spent

#### Scenario: Deep Breath unused
- **WHEN** a side casts Deep Breath and then plays an ordinary move
- **THEN** the protection is spent with no effect

### Requirement: Extra Turn
The system SHALL let a side that cast Extra Turn move again immediately after its move, without the opponent moving in between, unless that move ended the game. The opponent's pending reroll and lost-piece replacement SHALL resolve as if its skipped turn had begun. During the bonus turn the side SHALL NOT gain mana and SHALL NOT be able to cast a spell.

#### Scenario: Bonus turn
- **WHEN** White casts Extra Turn and moves
- **THEN** White is to move again
- **AND** a spell cast by White during that bonus turn is rejected

#### Scenario: First move ends the game
- **WHEN** White casts Extra Turn and then delivers checkmate
- **THEN** the game stays over and no bonus turn is given

### Requirement: Published state
The system SHALL publish, alongside the Card Chess section, both mana totals, whether each side currently sees the other's rank hand, and the list of active effects with the affected side, kind and square. A side's spell hand SHALL be sent only to that side. A side's rank hand and reroll marks SHALL be sent only to that side and to an opponent whose Peek is active. Available moves SHALL be sent only to the side to move. Once the game is over everything SHALL be sent to everyone.

#### Scenario: Cast flag
- **WHEN** the side to move has cast a spell this turn
- **THEN** the published state says so until that side's turn ends

#### Scenario: Opponent's view
- **WHEN** an update is built for White while no Peek is active
- **THEN** it contains White's spell hand and rank hand, and Black's are empty

#### Scenario: Spectator's view
- **WHEN** an update is built for a user without a seat during the game
- **THEN** both spell hands, both rank hands and the available moves are empty

#### Scenario: Game over
- **WHEN** an update is built after the game ended
- **THEN** it contains both sides' spell hands and rank hands

#### Scenario: Active effect listed
- **WHEN** Freeze Square is cast
- **THEN** the effect appears in the active effects until the opponent's turn resolves
