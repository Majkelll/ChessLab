# absorption-chess Specification

## Purpose
Describe Absorption Chess: standard chess in which a capturing piece gains the movement of the piece it captures.

## Requirements

### Requirement: Absorbing on capture
The system SHALL add the movement powers of a captured piece to the capturing piece, permanently. A piece SHALL never lose a power except pawn movement on promotion. The game SHALL be played by two seats from the standard chess starting position.

#### Scenario: Knight takes bishop
- **WHEN** a knight captures a bishop
- **THEN** the knight can afterwards move as a knight and as a bishop

#### Scenario: Combined moves offered
- **WHEN** a piece carries absorbed powers
- **THEN** its available moves include the moves of every power it carries

#### Scenario: King captures
- **WHEN** the king captures a rook
- **THEN** the king gains rook movement and remains the piece that must not be left in check

### Requirement: Promotion
The system SHALL replace a promoting pawn's pawn movement with the chosen piece's movement and keep any powers the pawn had absorbed.

#### Scenario: Pawn with absorbed knight power promotes to queen
- **WHEN** a pawn that absorbed a knight promotes to a queen
- **THEN** the piece moves as a queen and as a knight and no longer as a pawn

### Requirement: Pieces carrying pawn movement
The system SHALL treat any piece that carries pawn movement as a pawn for the pawn rules: it SHALL be able to step, double-step from its side's second rank and capture as a pawn, and any move that takes it to the last rank, by any of its powers, SHALL be a promotion.

#### Scenario: Fifty-move counter
- **WHEN** a piece carrying pawn movement moves by any of its powers
- **THEN** the count of half-moves without a pawn move or capture starts over

#### Scenario: Knight that absorbed a pawn reaches the last rank
- **WHEN** a knight that absorbed a pawn moves to the last rank by a knight move
- **THEN** the move is offered only with a promotion piece

### Requirement: Move legality
The system SHALL offer only moves that do not leave the mover's king attacked, taking absorbed powers into account for both movement and attacks. Castling SHALL follow standard chess rules and en passant SHALL follow the chess-rules capability.

#### Scenario: Unavailable move
- **WHEN** a move that is not available is submitted
- **THEN** the move is rejected with `<from>-<to> is not a legal move right now.`

### Requirement: Game end
The system SHALL end the game by Checkmate, and as a draw by Stalemate, by Repetition when the same placement, side to move and set of absorbed powers occurs three times, by FiftyMoveRule after 100 half-moves without a pawn move or capture, and by MoveLimit after 300 moves. Repetition SHALL be checked first and SHALL ignore castling and en passant rights. There SHALL be no insufficient-material draw. Resignation and timeout SHALL apply as in every mode.

#### Scenario: Checkmate
- **WHEN** a move leaves the opponent in check with no legal move
- **THEN** the game ends by Checkmate with the mover as winner

### Requirement: Published state
The system SHALL publish the position, whether the side to move is in check, and a powers text listing only pieces that carry powers beyond their own, as `<square>:<letters>` entries separated by commas, with letters in the order P, N, B, R, Q, K. The position text SHALL be the position followed by ` | ` and the powers text when any piece carries extra powers.

#### Scenario: No captures yet
- **WHEN** no capture has been made
- **THEN** the powers text is empty and the position text is the position alone

#### Scenario: Knight absorbs a rook
- **WHEN** a knight captures a rook on b3
- **THEN** the powers text is `b3:N`

### Requirement: Displayed piece kind
The system SHALL show a piece that carries several powers as its strongest kind, in the order queen, rook, bishop, knight, pawn, and SHALL always show the royal piece as a king. The same kind SHALL be used in the position and in move notation.

#### Scenario: Knight with rook movement
- **WHEN** a knight has absorbed a rook
- **THEN** the piece appears as a rook in the position, with knight listed as its extra power
