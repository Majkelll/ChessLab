# card-chess Specification

## Purpose
Describe Card Chess: standard chess where each side holds a hand of rank cards and may only move a piece kind for which it holds a card. These rules also underlie Arcane Chess.

## Requirements

### Requirement: Card to piece mapping
The system SHALL map ranks Two through Nine to the pawn, Ten to the knight, Jack to the bishop, Queen to the queen, King to the king and Ace to the rook.

#### Scenario: Pawn card
- **WHEN** a side holds a Seven
- **THEN** it may move any pawn that has a legal move

### Requirement: Decks and hands
The system SHALL give each of the two sides its own shuffled deck of the thirteen ranks and a hand of five cards. A drawn card SHALL be skipped when its piece kind has no legal move for that side or its rank is already in the hand. When thirteen draws find no such card, the draw SHALL settle for the next best card. When the draw pile is empty the discard pile SHALL be reshuffled into it. A deck SHALL always hold exactly thirteen cards, one per rank; the cards in a hand stay part of it.

#### Scenario: Opening hands
- **WHEN** a game starts
- **THEN** each side holds five different cards
- **AND** no card is for a piece kind that cannot move in the opening position

#### Scenario: Nothing playable to draw
- **WHEN** a card is drawn while no piece kind outside the hand has a legal move
- **THEN** the card drawn is one whose rank is not yet in the hand, or failing that a playable card, or failing that any card

#### Scenario: Both sides start healthy
- **WHEN** a game starts
- **THEN** each side has 3 HP and the game is not over

### Requirement: Moves allowed by the hand
The system SHALL make available to the side to move the legal chess moves of every piece kind for which it holds an unmarked card. A move that would capture a king SHALL never be available.

#### Scenario: Opening moves
- **WHEN** a game starts
- **THEN** White's available moves are the union of the legal moves for each card in White's hand

#### Scenario: Move outside the hand
- **WHEN** the side to move submits a legal chess move of a piece kind it holds no unmarked card for, while other cards are playable
- **THEN** the move is rejected with `<from>-<to> is not a legal move right now.`

### Requirement: Playing a card
The system SHALL discard the card that allowed a move and draw a replacement, keeping the hand at five cards. The card spent SHALL be the first unmarked card in hand order that allows the move, and the replacement SHALL be added at the end of the hand. The replacement MAY have the same rank as the card just played.

#### Scenario: Card consumed
- **WHEN** a side moves a knight using its Ten
- **THEN** the Ten leaves the hand and a new card is drawn
- **AND** the hand still holds five cards

#### Scenario: Marked and unmarked card of one kind
- **WHEN** a side holds two pawn cards, one marked for reroll, and moves a pawn
- **THEN** the unmarked card is spent

### Requirement: Reroll selection
The system SHALL let the side to move mark up to two different cards from its hand for reroll. A new selection SHALL replace the previous one, and an empty selection SHALL clear it. Marked cards SHALL NOT allow moves this turn and SHALL be exchanged for fresh cards when that side's next turn begins. A mark SHALL be dropped when its card is replaced for another reason.

#### Scenario: Marking cards
- **WHEN** a side marks two cards
- **THEN** the hand is unchanged and both cards are listed as pending reroll

#### Scenario: Selecting again
- **WHEN** a side marks one card and then marks a different card
- **THEN** only the second card is pending

#### Scenario: Too many cards
- **WHEN** a side marks three cards
- **THEN** the selection is rejected with `Can only mark up to 2 cards for reroll.`

#### Scenario: Selection without cards
- **WHEN** a reroll selection arrives without a list of cards
- **THEN** it is rejected with `A reroll selection needs a list of cards.`

#### Scenario: Duplicate or foreign card
- **WHEN** a side marks the same card twice or a card not in its hand
- **THEN** the selection is rejected with `Cannot mark the same card twice.` or `Can only mark cards that are currently in hand.`

#### Scenario: Reroll resolves
- **WHEN** a side marked cards, moved, and the opponent has moved
- **THEN** the marked cards have been replaced as that side's turn begins

#### Scenario: Marking every card of a kind
- **WHEN** a side marks all of its pawn cards
- **THEN** pawn moves are not available this turn

#### Scenario: Marking every card that could move
- **WHEN** only marked cards could produce a move
- **THEN** no moves are available, no free move is granted, and the side must change its selection

### Requirement: Reroll replacement
The system SHALL prefer a replacement card of a different piece kind than the rerolled card by drawing a second time when the first draw matches the kind, keeping the second draw only if its kind differs.

#### Scenario: First draw matches
- **WHEN** a Seven is rerolled, the first draw is an Eight and the second draw is a Jack
- **THEN** the replacement is the Jack

#### Scenario: Both draws match
- **WHEN** a Seven is rerolled and both draws are pawn cards
- **THEN** the replacement is the first draw

### Requirement: Cards for lost pieces
The system SHALL replace, at the start of a side's turn and after its pending rerolls are resolved, every card in its hand whose piece kind that side no longer has on the board. A card for a piece that is on the board but cannot move SHALL be kept.

#### Scenario: Last bishop captured
- **WHEN** a side's turn begins after its last bishop was captured while it holds a Jack
- **THEN** the Jack is replaced

### Requirement: No playable card
The system SHALL let the side to move play any legal move that does not capture a king when no card in its hand, marked or not, allows a move. The move SHALL NOT consume a card.

#### Scenario: Not in check
- **WHEN** the side to move holds no playable card and is not in check
- **THEN** every legal non-king-capturing move is available at no cost

#### Scenario: In check with HP left
- **WHEN** the side to move holds no playable card, is in check and has at least 1 HP
- **THEN** every legal move that does not capture a king is available as an emergency move
- **AND** playing one costs exactly 1 HP and leaves the hand untouched

#### Scenario: In check with no HP
- **WHEN** the side to move holds no playable card, is in check and has 0 HP
- **THEN** the game ends with reason HpDepleted and the opponent as winner

### Requirement: Game end
The system SHALL end the game by the standard chess endings, resignation, timeout or HP depletion. Checkmate detected by the chess rules SHALL take precedence over emergency handling. HP SHALL stay between 0 and 3. HP depletion SHALL be decided when a turn begins.

#### Scenario: Checkmate
- **WHEN** a move delivers checkmate
- **THEN** the game ends by Checkmate and no emergency move is offered

#### Scenario: Out of HP
- **WHEN** a side that has spent all 3 HP needs another emergency move
- **THEN** it loses at the start of that turn, not when it spent its last HP

#### Scenario: Only king captures remain
- **WHEN** the side to move holds no playable card and every legal move would capture the opposing king
- **THEN** the game ends by Checkmate with the side to move as winner

### Requirement: Published state
The system SHALL publish both hands, both sets of cards pending reroll, both HP totals, whether the side to move has no playable card, and whether an emergency move is available, identically to every recipient. Hands are public in Card Chess; Arcane Chess narrows this.

#### Scenario: Opponent's hand
- **WHEN** a Card Chess update is sent
- **THEN** it contains the opponent's hand and reroll marks as well as the recipient's own

### Requirement: Clock
The system SHALL deduct the elapsed time from the mover's clock on each move and add the increment unless the chess rules ended the game.

#### Scenario: Move with increment
- **WHEN** a side moves after 5 seconds with a 2 second increment
- **THEN** its clock drops by 3 seconds net
