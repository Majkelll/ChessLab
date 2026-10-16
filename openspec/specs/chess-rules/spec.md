# chess-rules Specification

## Purpose
Describe the two chess rule engines the modes are built on and the position, square and move formats they share. Rules that belong to one mode live in the capability named after that mode.

## Requirements

### Requirement: Two rule engines
The system SHALL use the Gera.Chess library, with all of its automatic endings enabled, for Hand and Brain, Card Chess, Arcane Chess and Progressive Chess, and its own board for Bidding, Alice, Absorption and Draft Chess. Martian Chess uses neither.

#### Scenario: Library endings
- **WHEN** a library-backed mode reaches checkmate, stalemate, insufficient material, the fifty-move rule or a repetition
- **THEN** the game ends with the matching reason without any mode-specific code

#### Scenario: Repetition after a loaded position
- **WHEN** a position is loaded into the library engine during a game, as Arcane Chess board spells and Progressive Chess series do
- **THEN** the repetition count starts over from that position

#### Scenario: Own board has no endings
- **WHEN** a mode on the system's own board needs an ending
- **THEN** the mode decides it from the board's moves, counters and position

### Requirement: Own board agrees with standard chess
The system's own board SHALL produce the same legal moves and positions as the Gera.Chess library for standard chess, including castling, en passant and promotion.

#### Scenario: Known move counts
- **WHEN** all move sequences of depth 1, 2 and 3 are counted from the standard starting position
- **THEN** the counts are 20, 400 and 8902

#### Scenario: Random games
- **WHEN** seeded random games are played on both engines
- **THEN** the legal moves and the resulting position agree after every half-move

### Requirement: Legal and unchecked moves
The system's own board SHALL offer legal moves, which never capture a king and never leave the mover's king attacked, and unchecked moves, which ignore check and include king captures. Absorption and Draft Chess SHALL use legal moves; Bidding and Alice Chess SHALL start from unchecked moves.

#### Scenario: Pinned piece
- **WHEN** a move would leave the mover's king attacked
- **THEN** it is absent from the legal moves

#### Scenario: King capture
- **WHEN** a piece attacks the opposing king
- **THEN** the capture is among the unchecked moves

### Requirement: En passant on the own board
The system's own board SHALL create an en passant opportunity only when a piece with pawn movement advances two squares along its file from its side's second rank, and SHALL let it be taken only by a one-square diagonal pawn step that removes an opposing piece with pawn movement standing beside the capturer.

#### Scenario: Two-rank move elsewhere
- **WHEN** a rook that also carries pawn movement moves from a4 to a6
- **THEN** no en passant opportunity is created

#### Scenario: Long diagonal onto the skipped square
- **WHEN** a bishop that also carries pawn movement moves several squares onto the square a pawn just skipped
- **THEN** nothing is captured

### Requirement: Promotion choices
The system SHALL offer a move to the last rank by a pawn as four separate moves, promoting to queen, rook, bishop and knight, in both engines.

#### Scenario: Pawn on the seventh rank
- **WHEN** a white pawn can advance to the eighth rank
- **THEN** four moves with that origin and destination are offered, one per promotion piece

### Requirement: Direct position edits
The system SHALL support moving, removing and swapping pieces directly in a position string. Every such edit SHALL clear any en passant opportunity and revoke the castling rights tied to a king or rook home square it touches, and SHALL leave the side to move and the move counters unchanged.

#### Scenario: Rook moved off its home square
- **WHEN** an edit moves the piece on h1
- **THEN** White's king-side castling right is revoked

#### Scenario: Source square empty
- **WHEN** an edit moves from an empty square, or swaps with either square empty
- **THEN** it fails with a message starting `No piece at <square>.`

#### Scenario: Changing the side to move
- **WHEN** the side to move of a position is rewritten
- **THEN** the en passant opportunity is cleared

### Requirement: Squares and moves on the wire
The system SHALL identify a square by a file and a rank numbered 0 to 7 and write it as a lowercase letter and digit such as `e4`. A move SHALL carry its origin, destination, moving piece kind, captured piece kind, promotion piece, check and checkmate flags and its notation. Squares, moves and game state SHALL travel over the hub in the MessagePack protocol.

#### Scenario: Square text
- **WHEN** the square with file 4 and rank 3 is written
- **THEN** the text is `e4`

#### Scenario: Malformed square text
- **WHEN** a square is parsed from text that is not two characters long
- **THEN** parsing fails with `Invalid square notation: '<text>'.`
