# alice-chess Specification

## Purpose
Describe Alice Chess: chess on two boards, where every move sends the moved piece to the same square on the other board.

## Requirements

### Requirement: Two boards
The system SHALL start with the standard chess position on board A and an empty board B, White to move.

#### Scenario: Opening state
- **WHEN** a game starts
- **THEN** all thirty-two pieces stand on board A and board B is empty

### Requirement: Transfer on every move
The system SHALL move a piece on the board it stands on and then place it on the same square of the other board. A capture SHALL remove the captured piece from the board the move was made on.

#### Scenario: First move
- **WHEN** White plays the e-pawn from e2 to e4 on board A
- **THEN** e2 and e4 are empty on board A and the pawn stands on e4 on board B

#### Scenario: Pieces stay on one board each
- **WHEN** any number of moves has been played
- **THEN** no square is occupied on both boards

### Requirement: Move legality
The system SHALL offer moves for the pieces on both boards. A move SHALL be available only when it follows chess movement on its own board, its destination square is empty on the other board, it does not capture a king, it does not leave the mover's king attacked on the board the move is played on, and the mover's king is not attacked on the board it stands on after the transfer. Castling and en passant SHALL NOT be available.

#### Scenario: Promotion
- **WHEN** a pawn reaches the last rank
- **THEN** it promotes to a queen, rook, bishop or knight and the promoted piece transfers like any other

#### Scenario: Pawn's double step
- **WHEN** a pawn advances two squares
- **THEN** only its destination has to be empty on the other board, not the square it passes

#### Scenario: Destination occupied on the other board
- **WHEN** a piece could move to a square that is occupied on the other board
- **THEN** that move is not available

#### Scenario: Castling
- **WHEN** the king and rook have not moved and the squares between them are empty
- **THEN** castling is not offered

#### Scenario: King left attacked
- **WHEN** a move would leave the mover's king attacked on its own board
- **THEN** that move is not available

#### Scenario: King stepping onto a square attacked on its own board
- **WHEN** a king move would arrive on a square attacked on the board it is played on, even though that square is safe on the other board
- **THEN** that move is not available

#### Scenario: King stepping onto a covered square
- **WHEN** a king move would arrive on a square attacked on the other board
- **THEN** that move is not available

### Requirement: Game end
The system SHALL end the game by Checkmate when the side to move has no move and its king is attacked, as a draw by Stalemate when it has no move and its king is not attacked, and as a draw by MoveLimit after 300 moves. No other draw rule applies. Resignation and timeout SHALL apply as in every mode.

#### Scenario: Checkmate
- **WHEN** a move uncovers an attack the opposing king cannot escape
- **THEN** the game ends by Checkmate with the mover as winner

### Requirement: Published state and notation
The system SHALL publish each board's position, whether the side to move is in check, and a position text of both boards separated by ` | `. Each move SHALL be recorded as its algebraic notation, without check or mate marks, followed by the board it was played on in parentheses.

#### Scenario: Board position strings
- **WHEN** the two board positions are published
- **THEN** the side to move of the game update is authoritative, not the side-to-move field inside either board's position string

#### Scenario: Notation
- **WHEN** White opens with e4 from board A
- **THEN** the recorded notation is `e4(A)`
