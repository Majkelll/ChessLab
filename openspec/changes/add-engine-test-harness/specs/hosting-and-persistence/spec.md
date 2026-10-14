# Spec Delta

## ADDED Requirements

### Requirement: Engine checks in continuous integration
The system SHALL run the engine gates of the engine-testing capability on every pull request against `main` on the self-hosted runners, and SHALL make the strength check required for merging. The SPRT SHALL run on a runner labelled `chesslab-sprt` and report its outcome as the pull request's strength check.

#### Scenario: Fast gates
- **WHEN** a pull request targets `main`
- **THEN** perft, differential fuzzing, invariants, tactics and bench run alongside the existing tests

#### Scenario: SPRT result
- **WHEN** an SPRT for a pull request finishes
- **THEN** the strength check is set to passed for `H1 accepted` and failed otherwise, with the report as its summary
