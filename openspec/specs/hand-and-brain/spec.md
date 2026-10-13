# hand-and-brain Specification

## Purpose
Describe Hand and Brain: standard chess played by two teams of two, where the Brain names a piece kind and the Hand must move a piece of that kind.

## Requirements

### Requirement: Two-phase turn
The system SHALL split every turn into a Brain phase and a Hand phase. The active seat SHALL be the Brain of the side to move during the Brain phase and the Hand of the side to move during the Hand phase. A game SHALL start in the Brain phase with White to move. The mode SHALL be played from the standard chess starting position by four seats: a Brain and a Hand for each side.

#### Scenario: Opening seat
- **WHEN** a game starts
- **THEN** the only active seat is White Brain

#### Scenario: After the announcement
- **WHEN** White Brain announces a piece kind
- **THEN** the only active seat is White Hand

#### Scenario: After the move
- **WHEN** White Hand completes a move
- **THEN** the phase returns to Brain and the only active seat is Black Brain

### Requirement: Brain announcement
The system SHALL let the Brain announce only a piece kind that has at least one legal move for its side, and SHALL publish the announceable kinds during the Brain phase. An announcement SHALL NOT be changed once made.

#### Scenario: Opening choices
- **WHEN** a game starts
- **THEN** the announceable kinds are Pawn and Knight only

#### Scenario: Kind without a legal move
- **WHEN** White Brain announces Queen on the first move
- **THEN** the announcement is rejected with `Queen has no legal move for White.`

#### Scenario: Announcing in the Hand phase
- **WHEN** the Hand, whose seat is active, sends an announcement
- **THEN** it is rejected with `Expected phase BrainSelecting but was HandMoving.`

#### Scenario: Brain announces again
- **WHEN** the Brain sends an announcement while the Hand is to move
- **THEN** it is rejected with `It's not your turn.`

#### Scenario: In check
- **WHEN** the side to move is in check
- **THEN** only piece kinds that can answer the check are announceable

### Requirement: Hand move
The system SHALL let the Hand play only a legal chess move of the announced piece kind, and SHALL publish those moves as the available moves during the Hand phase. No moves SHALL be available during the Brain phase.

#### Scenario: Move of another kind
- **WHEN** the Brain announced Knight and the Hand submits a pawn move
- **THEN** the move is rejected with `<from>-<to> is not a legal move for the announced piece kind (Knight).`

#### Scenario: Move of the announced kind
- **WHEN** the Brain announced Pawn and the Hand plays e2-e4
- **THEN** the move is applied, added to the move history, and the announcement is cleared

### Requirement: Announcement visibility
The system SHALL publish the announced piece kind to everyone in the room, and the client SHALL show it to the Hand of the same team and to the opposing team.

#### Scenario: Hand sees the announcement
- **WHEN** the Brain announces a piece kind
- **THEN** the Hand's screen shows that kind

#### Scenario: Opponents see the pick
- **WHEN** a team's Brain has announced
- **THEN** the opposing team sees a read-only view of that pick

### Requirement: Team clock
The system SHALL run one clock per side that covers both the Brain's and the Hand's thinking time. The elapsed time SHALL be deducted when the Hand moves, and the increment SHALL be added unless that move ended the game.

#### Scenario: Slow Brain
- **WHEN** the Brain takes 20 seconds to announce and the Hand 5 seconds to move
- **THEN** 25 seconds are deducted from that side's clock

### Requirement: Game end
The system SHALL end the game by checkmate, stalemate, insufficient material, the fifty-move rule, repetition, resignation or timeout, using standard chess rules. Only the side to move can run out of time. After the game ends no piece kinds and no moves SHALL be published as available.

#### Scenario: Fool's mate
- **WHEN** the moves f3, e5, g4, Qh4 are played through announcements and moves
- **THEN** the game ends by Checkmate with Black as winner

#### Scenario: Side to move flags
- **WHEN** the side to move has no time left
- **THEN** the game ends by Timeout with the other side as winner
