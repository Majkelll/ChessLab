# Spec Delta

## ADDED Requirements

### Requirement: Bot service check
The system SHALL include a check named `bots` that reports the outcome of the latest bot service health probe: Healthy with the service's version and the probe latency when it succeeded, Degraded otherwise. Failure details SHALL NOT be included in the report.

#### Scenario: Bot service reachable
- **WHEN** the latest probe succeeded
- **THEN** `checks.bots.status` is `Healthy` and `data` contains `latencyMs` and `serviceVersion`

#### Scenario: Bot service down
- **WHEN** the latest probe failed while the database is reachable
- **THEN** `checks.bots.status` is `Degraded` with description `Bot service is unreachable.`, the overall `status` is `Degraded` and the response status is 200

#### Scenario: Bot service not configured
- **WHEN** no bot service address is configured
- **THEN** `checks.bots.status` is `Degraded` with description `Bot service is not configured.`
