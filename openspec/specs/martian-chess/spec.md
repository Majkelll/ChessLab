# martian-chess Specification

## Purpose
Describe Martian Chess: pyramids on a board four files wide and eight ranks tall, where a side owns whatever stands in its half of the board.

## Requirements

### Requirement: Board and setup
The system SHALL use a 4 by 8 board split into a White half (ranks 1 to 4) and a Black half (ranks 5 to 8). Each half SHALL start with nine pyramids in a 3 by 3 corner block (a1 to c3 for White, mirrored to b6 to d8 for Black): three worth 3, three worth 2 and three worth 1, with the most valuable nearest the corner. Both scores SHALL start at 0 and White SHALL move first.

#### Scenario: Opening state
- **WHEN** a game starts
- **THEN** each half holds nine pyramids and both scores are 0

### Requirement: Ownership by position
The system SHALL let a side move only pyramids standing in its own half. A pyramid that moves into the other half SHALL belong to the opponent from then on.

#### Scenario: Moves start in the mover's half
- **WHEN** White is to move
- **THEN** every available move starts on ranks 1 to 4

#### Scenario: Crossing the middle
- **WHEN** White moves a pyramid from rank 4 to rank 5
- **THEN** Black can move that pyramid on its turn

### Requirement: Pyramid movement
The system SHALL move a pyramid worth 1 one square diagonally, a pyramid worth 2 one or two squares orthogonally, and a pyramid worth 3 any distance orthogonally or diagonally. Pyramids worth 2 and 3 SHALL NOT jump over other pyramids; the first pyramid on their path is the last square they can reach.

#### Scenario: Pawn
- **WHEN** a pyramid worth 1 is to move
- **THEN** its moves are single diagonal steps

#### Scenario: Drone
- **WHEN** a pyramid worth 2 is to move with a clear path
- **THEN** it reaches squares one or two steps away in a straight line

### Requirement: Capturing and scoring
The system SHALL let a pyramid capture only a pyramid in the opponent's half, and SHALL add the captured pyramid's value to the mover's score. A move SHALL NOT land on a pyramid in the mover's own half.

#### Scenario: Capture
- **WHEN** White captures a pyramid worth 3 in Black's half
- **THEN** White's score increases by 3

#### Scenario: Own half is blocked
- **WHEN** a destination in the mover's half holds a pyramid
- **THEN** that move is not available

### Requirement: No immediate reversal
The system SHALL NOT let a side move a pyramid straight back along the move the opponent just played.

#### Scenario: Pushing a piece back
- **WHEN** Black moved a pyramid from b5 to b4
- **THEN** White cannot move that pyramid from b4 to b5 on its next turn

### Requirement: No promotion
The system SHALL reject any move that names a promotion piece with `There is no promotion in Martian Chess.`

#### Scenario: Promotion requested
- **WHEN** a move action names a promotion piece
- **THEN** the move is rejected and the position is unchanged

### Requirement: Game end
The system SHALL end the game with reason BoardHalfEmptied when either half holds no pyramid after a move, with reason Stalemate when the side to move has no move, and with reason MoveLimit after 300 moves. In each case the side with the higher score SHALL win and equal scores SHALL be a draw. Resignation and timeout SHALL apply as in every mode and give the win to the opponent whatever the score.

#### Scenario: Half emptied
- **WHEN** a move leaves one half of the board empty and White leads 12 to 9
- **THEN** the game ends with reason BoardHalfEmptied and White as winner

#### Scenario: Equal scores
- **WHEN** the game ends with both scores equal
- **THEN** there is no winner

### Requirement: Published state and notation
The system SHALL publish every pyramid's square and value and both scores. The position text SHALL list ranks from 8 to 1 separated by `/`, using the pyramid's value or `.` for an empty square, followed by a space, `W` or `B` for the side to move, a space and the score as `<white>-<black>`. Moves SHALL be recorded as `P`, `D` or `Q` for values 1, 2 and 3 (reported to clients and bots as pawn, knight and queen), the origin square, `-` or `x` for a capture, and the destination square.

#### Scenario: Opening position text
- **WHEN** a game starts
- **THEN** the position text is `.233/.123/.112/..../..../211./321./332. W 0-0`

#### Scenario: Drone captures
- **WHEN** a pyramid worth 2 moves from b3 to b5 capturing
- **THEN** the recorded notation is `Db3xb5`
