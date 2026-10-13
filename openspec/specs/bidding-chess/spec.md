# bidding-chess Specification

## Purpose
Describe Bidding Chess: there are no alternating turns. Before every move both sides bid chips in secret, and the higher bidder pays the opponent and moves.

## Requirements

### Requirement: Chips and marker
The system SHALL start each side with 100 chips and give White the tie-break marker. The game SHALL be played by two seats from the standard chess starting position, and the two sides' chips SHALL always total 200.

#### Scenario: Opening state
- **WHEN** a game starts
- **THEN** both sides hold 100 chips, White holds the marker and the phase is Bidding

### Requirement: Bidding phase
The system SHALL accept one bid per side per round, between 0 and that side's chips. Both sides SHALL be active until they have bid. A side's bid amount SHALL NOT be published until both have bid; only the fact that it has bid is published. No moves SHALL be available during the Bidding phase.

#### Scenario: Both sides active
- **WHEN** a bidding round opens
- **THEN** both the White and the Black seat are active

#### Scenario: One side has bid
- **WHEN** White has bid and Black has not
- **THEN** only the Black seat is active

#### Scenario: Bidding twice
- **WHEN** a side bids a second time in the same round
- **THEN** the bid is rejected with `<side> has already bid this turn.`

#### Scenario: Bid above holdings
- **WHEN** a side holding 100 chips bids 101, or bids a negative amount
- **THEN** the bid is rejected with `<side> can bid between 0 and 100 chips.`

#### Scenario: Bid while a move is pending
- **WHEN** a side bids during the Moving phase
- **THEN** the bid is rejected with `Bids are closed until this move has been played.`

### Requirement: Bid resolution
The system SHALL give the move to the higher bidder once both sides have bid. The winner SHALL pay its bid to the opponent. On a tie the marker holder SHALL win and the marker SHALL pass to the other side. Both bids SHALL then be published as the last bids, where they stay until the next round resolves, and the phase SHALL become Moving.

#### Scenario: Higher bid wins
- **WHEN** White bids 30 and Black bids 10
- **THEN** White is to move, White holds 70 chips and Black holds 130

#### Scenario: Tied bids
- **WHEN** both sides bid 20 while White holds the marker
- **THEN** White is to move and pays 20 chips
- **AND** Black now holds the marker

#### Scenario: Winning twice in a row
- **WHEN** the same side wins two consecutive rounds
- **THEN** that side moves twice in a row

### Requirement: Moving phase
The system SHALL let only the bid winner move, and SHALL offer every move the pieces can make without regard to check, including moves that leave the mover's king attacked and moves that capture the opposing king. Castling SHALL still require that the king does not start on, pass over or arrive on an attacked square. After the move the phase SHALL return to Bidding.

#### Scenario: Move before bids are in
- **WHEN** a move is submitted during the Bidding phase
- **THEN** the move is rejected with `Both sides have to bid before a move can be played.`

#### Scenario: Unavailable move
- **WHEN** the bid winner submits a move that is not available
- **THEN** the move is rejected with `<from>-<to> is not a move <side> can play right now.`

#### Scenario: Same side moves twice after a double pawn step
- **WHEN** White plays e2-e4 and wins the next bid as well
- **THEN** White's pawns on d2 and f2 are not offered a move to e3

#### Scenario: King left attacked
- **WHEN** the bid winner's king is attacked
- **THEN** moves that leave the king attacked are still available

### Requirement: Game end
The system SHALL end the game when a king is captured, with the capturer as winner and reason KingCaptured. The game SHALL be drawn with reason Stalemate when the bid winner has no move, FiftyMoveRule after 100 half-moves without a pawn move or capture, and MoveLimit after 300 moves. There SHALL be no checkmate, repetition or insufficient-material ending. Resignation and timeout SHALL apply as in every mode.

#### Scenario: King captured
- **WHEN** the bid winner captures the opposing king
- **THEN** the game ends with reason KingCaptured and the mover as winner

#### Scenario: Bid winner cannot move
- **WHEN** bids resolve and the winner has no move
- **THEN** the game ends as a draw with reason Stalemate

### Requirement: Clock
The system SHALL run both clocks while both sides owe a bid, each measured from the start of the bidding round, and only the mover's clock during the Moving phase. The elapsed time SHALL be deducted when a side bids and when it moves, and the increment SHALL be added after a move that does not capture the king.

#### Scenario: Second bidder
- **WHEN** White bids 5 seconds into a round and Black 12 seconds into it
- **THEN** White is charged 5 seconds and Black 12 seconds

#### Scenario: Bid still owed at timeout
- **WHEN** a side that has not bid runs out of time
- **THEN** the game ends by Timeout with the other side as winner

### Requirement: Notation
The system SHALL record each move as its algebraic notation followed by the winning bid in parentheses, without check or mate marks.

#### Scenario: Move won with a bid of 30
- **WHEN** White wins the round with 30 chips and plays e4
- **THEN** the recorded notation is `e4(30)`
