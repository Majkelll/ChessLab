# Spec Delta

## Purpose

Describe how bot engines are measured for correctness and strength, and the automated gates that stop rule bugs and strength regressions from being merged.

## ADDED Requirements

### Requirement: Refereed matches
The system SHALL provide a match runner that plays two bot versions against each other in a chosen mode, with ChessLab.Core sessions applying every action and deciding the result, and each bot receiving only the state built for its own seat.

#### Scenario: Illegal action
- **WHEN** a bot returns an action the session rejects
- **THEN** that game is recorded as a loss for that bot with reason `illegal action`, and the match report counts it

#### Scenario: Bot crash or time overrun
- **WHEN** a bot throws or exceeds its time budget by more than 100 percent
- **THEN** that game is recorded as a loss for that bot and the match report counts it

#### Scenario: Anchor start-up
- **WHEN** a bot runs as a separate anchor process
- **THEN** the process starts and warms up before the game, outside its time budget, so an anchor of the same build as the candidate scores 50 percent with no time losses

### Requirement: Paired games
The match runner SHALL play games in pairs from the same opening and the same random seed for every shuffle and draw, with colours swapped in the second game. Openings SHALL come from the mode's opening suite in order.

#### Scenario: Card Chess pair
- **WHEN** a Card Chess pair is played
- **THEN** both games deal the same decks in the same order, and each bot plays each colour once

#### Scenario: Openings exhausted
- **WHEN** every opening in the suite has been used
- **THEN** the runner starts again from the first opening with new seeds

### Requirement: Sequential probability ratio test
The system SHALL decide whether a candidate is stronger than a baseline with an SPRT on pair results, with bounds given in Elo, alpha 0.05 and beta 0.05, and SHALL stop as soon as the test accepts either hypothesis or a game limit is reached. The report SHALL state the outcome, games played and the Elo difference with its 95 percent interval.

#### Scenario: Clear improvement
- **WHEN** the candidate wins clearly more pairs than it loses
- **THEN** the test stops early with outcome `H1 accepted`

#### Scenario: Game limit
- **WHEN** neither hypothesis is accepted within the game limit
- **THEN** the outcome is `inconclusive`

### Requirement: Bench fingerprint
Every engine SHALL provide a bench run that searches its mode's bench positions to a fixed depth on one thread and prints the total node count and nodes per second. For a given engine version the node count SHALL be identical on every run and machine.

#### Scenario: Repeat run
- **WHEN** bench runs twice on the same commit
- **THEN** both runs print the same node count

#### Scenario: Behaviour change
- **WHEN** a change alters search or evaluation
- **THEN** the node count differs from the one recorded for the previous version

### Requirement: Move generation correctness
Every engine with its own move generator SHALL match ChessLab.Core on perft reference counts for its mode's positions and on differential fuzzing, where for every position reached in random games the engine's legal actions equal the actions Core makes available.

#### Scenario: Perft mismatch
- **WHEN** an engine's perft count at some depth differs from the reference
- **THEN** the check fails and reports the position, the depth and the first move whose subtree differs

#### Scenario: Differential mismatch
- **WHEN** fuzzing reaches a position where engine and Core disagree on legal actions
- **THEN** the check fails and reports the position text and the differing actions

### Requirement: Engine invariants
Every engine SHALL keep its state consistent: undoing an action restores the exact previous state and hash, an incrementally updated hash equals the hash computed from scratch, and a search allocates no memory per node after warm-up. Where a mode is colour-symmetric, the evaluation of a mirrored position SHALL be the negation of the original.

#### Scenario: Make and unmake
- **WHEN** any legal action is applied and undone
- **THEN** the state and hash are identical to before

#### Scenario: Allocation in search
- **WHEN** a fixed-depth search runs after warm-up
- **THEN** the allocated byte count measured for the search thread does not grow

### Requirement: Tactics suites
Each mode SHALL have a tactics suite of positions with the expected action, and the system SHALL report how many an engine solves within a fixed time per position.

#### Scenario: Solved count drops
- **WHEN** an engine solves fewer positions of its suite than the version on `main`
- **THEN** the tactics check fails and lists the positions it no longer solves

### Requirement: Difficulty calibration
The system SHALL measure the Elo gap between consecutive difficulty levels of a mode by matches, and SHALL report a calibration failure when a level is not stronger than the one below it.

#### Scenario: Levels out of order
- **WHEN** Hard scores 50 percent or less against Medium
- **THEN** calibration fails for that mode

### Requirement: Engine gates on pull requests
On every pull request, the system SHALL run perft, differential fuzzing, invariants, tactics and bench for every engine, and SHALL fail when nodes per second fall more than 10 percent below `main`. When a bench node count differs from `main`, the pull request SHALL require a passing SPRT against `main` with bounds 0 and 5 Elo before it can be merged.

#### Scenario: Refactor
- **WHEN** a pull request changes engine code but no bench node count
- **THEN** no SPRT is required and the fast gates decide

#### Scenario: Strength change
- **WHEN** a pull request changes Martian Chess's bench node count
- **THEN** an SPRT for Martian Chess runs and the pull request cannot be merged until it reports `H1 accepted`

#### Scenario: Regression rejected
- **WHEN** the SPRT reports `H0 accepted` or `inconclusive`
- **THEN** the strength check fails

### Requirement: Nightly anchor matches and blunder mining
The system SHALL, every night on the home server at lowered CPU priority and without slowing live bot requests, play the current engines against the anchors and add to a review list every position from finished games where a deep search finds that a bot's move lost much more than its alternatives.

#### Scenario: Live traffic
- **WHEN** the bot service has queued requests during the nightly job
- **THEN** the nightly job pauses its games until the queue is empty

#### Scenario: Blunder found
- **WHEN** a deep search of a finished game finds a bot move that loses at least 200 centipawn-equivalent more than the best alternative
- **THEN** the position and the better action are written to the mode's review list
