# progressive-chess Specification

## Purpose
Describe Progressive Chess: standard chess in which each turn is a series of moves that grows by one every turn.

## Requirements

### Requirement: Growing series
The system SHALL let White play a series of one move, Black a series of two, White a series of three, and so on. The side to move SHALL stay the same until its series ends.

#### Scenario: White's first series
- **WHEN** White plays one move at the start
- **THEN** Black is to move and the series number is 2

#### Scenario: Black's first series
- **WHEN** Black plays the first move of series 2
- **THEN** Black is still to move with one move left in the series

### Requirement: Series end
The system SHALL end a series when its last move is played, when a move gives check, or when the mover has no legal move left for the rest of the series.

#### Scenario: Check ends the series
- **WHEN** a side gives check with moves still left in its series
- **THEN** the series ends and the opponent is to move

#### Scenario: Check must be answered first
- **WHEN** a side begins a series in check
- **THEN** only moves that escape the check are available for the first move of the series

### Requirement: Moves
The system SHALL offer the standard legal chess moves for the side to move. En passant SHALL only be possible on the first move of a series.

#### Scenario: Move out of turn
- **WHEN** a move for the side not on move is submitted
- **THEN** the move is rejected with `<from>-<to> is not a legal move right now.`

### Requirement: Game end
The system SHALL end the game by the standard chess endings (checkmate, stalemate, insufficient material, fifty-move rule, repetition), resignation or timeout, and SHALL declare a draw with reason MoveLimit when series number 150 has been completed. Repetition SHALL be counted only within one series (see chess-rules).

#### Scenario: Mate inside a series
- **WHEN** a move in a series delivers checkmate
- **THEN** the game ends by Checkmate with the mover as winner

### Requirement: Clock
The system SHALL deduct the elapsed time from the mover's clock on every move and add the increment once per series, when the series ends and the game continues.

#### Scenario: Three-move series
- **WHEN** a side completes a three-move series with a 5 second increment
- **THEN** 5 seconds are added once

### Requirement: Published state and notation
The system SHALL publish the series number, the moves played in the series and the moves left. The first move of each series SHALL be recorded with the series number as a prefix.

#### Scenario: Game ends inside a series
- **WHEN** a move ends the game
- **THEN** the series number and the moves played in it stay as they were

#### Scenario: Notation
- **WHEN** White plays e4 and Black replies e5 then Nf6
- **THEN** the recorded notations are `1.e4`, `2.e5`, `Nf6`
