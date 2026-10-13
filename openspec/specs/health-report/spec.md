# health-report Specification

## Purpose
Describe the public `/hc` endpoint that reports whether the application and its database are usable.

## Requirements

### Requirement: Public JSON health report
The system SHALL serve a JSON health report at `GET /hc` without requiring authentication. The report SHALL contain `status`, `version`, `uptimeSeconds`, `durationMs` and a `checks` object keyed by check name, where each check carries `status`, `description`, `durationMs` and `data`. The response status SHALL be 200 for a Healthy or Degraded report and 503 for an Unhealthy one.

#### Scenario: Anonymous caller reads a healthy report
- **WHEN** an unauthenticated client requests `GET /hc` while the database is reachable
- **THEN** the response status is 200
- **AND** `status` is `Healthy`
- **AND** `checks.database.status` is `Healthy`

#### Scenario: Version falls back when unknown
- **WHEN** the assembly has no informational version
- **THEN** `version` is `unknown`

#### Scenario: Uptime
- **WHEN** the report is produced
- **THEN** `uptimeSeconds` is the number of seconds since the process started

### Requirement: Database check
The system SHALL include a check named `database` that reports connectivity, the latency of the connectivity probe and the database provider, and SHALL report schema drift for PostgreSQL. Failure details SHALL NOT be included in the report.

#### Scenario: Database cannot be reached
- **WHEN** the database refuses the connection
- **THEN** the `database` check is `Unhealthy` with description `Cannot connect to the database.`

#### Scenario: Database check throws
- **WHEN** probing the database raises an exception
- **THEN** the `database` check is `Unhealthy` with description `Database health check threw an exception.`

#### Scenario: SQLite database is reachable
- **WHEN** the application runs on SQLite and the database is reachable
- **THEN** the `database` check is `Healthy` with description `Connected.`
- **AND** `data` contains `latencyMs` and `provider` equal to `sqlite`
- **AND** `data` contains no migration counts

#### Scenario: PostgreSQL schema is up to date
- **WHEN** the application runs on PostgreSQL with no pending migrations
- **THEN** the `database` check is `Healthy`
- **AND** `data` contains `provider` equal to `postgres`, `appliedMigrations` and `pendingMigrations` equal to 0

#### Scenario: PostgreSQL schema is behind
- **WHEN** the application runs on PostgreSQL and at least one migration is pending
- **THEN** the `database` check is `Degraded` with description `Connected, but the schema is behind the application.`
