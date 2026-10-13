# authentication Specification

## Purpose
Describe how visitors sign in and out, how a signed-in visitor is identified, and which parts of the application require a signed-in user.

## Requirements

### Requirement: Google sign-in
The system SHALL authenticate users through Google OAuth (apart from the test-only sign-in below) and SHALL keep the session in an authentication cookie. `GET /Account/Login` SHALL issue a Google challenge that returns to `returnUrl` when it is a local path, and to `/` otherwise. The OAuth callback path SHALL be `/signin-google`. The Google profile picture SHALL be carried in the session as claim `urn:google:picture`, and all session claims SHALL be made available to the browser client.

#### Scenario: Login without a return URL
- **WHEN** a visitor requests `GET /Account/Login`
- **THEN** the visitor is redirected to Google sign-in
- **AND** a successful sign-in returns the visitor to `/`

#### Scenario: User record changes during a session
- **WHEN** a signed-in user's record changes
- **THEN** the session keeps the claims it was issued with until the user signs in again

#### Scenario: Login with an external return URL
- **WHEN** a visitor requests `GET /Account/Login?returnUrl=https://example.com` or `returnUrl=//example.com`
- **THEN** a successful sign-in returns the visitor to `/`

#### Scenario: Anonymous visitor opens a protected page
- **WHEN** an anonymous visitor opens `/room/AAAAAA` or `/game/AAAAAA`
- **THEN** the visitor is challenged for Google sign-in

### Requirement: User record upsert on sign-in
The system SHALL create or update a user record on every Google sign-in, keyed by the Google subject identifier, and SHALL add a `buid` claim holding the user's internal identifier to the session.

#### Scenario: First sign-in
- **WHEN** a Google account signs in for the first time
- **THEN** a user is stored with a new identifier, the Google subject identifier, email, display name and avatar URL
- **AND** creation time and last login time are both set to the current time

#### Scenario: Returning sign-in
- **WHEN** a Google account that already has a user record signs in
- **THEN** email, display name and avatar URL are overwritten with the values from Google
- **AND** last login time is updated
- **AND** the user identifier and creation time are unchanged

#### Scenario: Google supplies no name
- **WHEN** the Google profile has no name claim
- **THEN** the email is stored as the user's display name

#### Scenario: Google supplies no email
- **WHEN** the Google profile has no email claim
- **THEN** an empty string is stored as the email

### Requirement: Sign-out
The system SHALL sign the user out on `POST /Account/Logout` and redirect to the `returnUrl` query value when it is a local path, and to `/` otherwise. The endpoint requires neither an authenticated caller nor an antiforgery token.

#### Scenario: Logging out
- **WHEN** a signed-in user submits the log out form
- **THEN** the authentication cookie is cleared
- **AND** the user lands on the anonymous landing page

### Requirement: Protected surfaces
The system SHALL require an authenticated user for the game hub at `/hubs/game` and for every page except the landing page, the not-found page and the error page. `GET /hc` is public. Protection is declared per page; there is no fallback policy.

#### Scenario: Anonymous landing page
- **WHEN** an anonymous visitor opens `/`
- **THEN** the landing pitch and a `Sign in with Google` link are shown

#### Scenario: Signed-in header
- **WHEN** a signed-in user views any page
- **THEN** the user's display name, their Google picture when the session carries one, and a `Log out` button are shown

#### Scenario: In-app navigation without a session
- **WHEN** the browser client routes to a protected page without an authenticated user
- **THEN** the page shows `You need to sign in to view this page.` and a `Sign in with Google` link

### Requirement: Test-only sign-in
The system SHALL expose `GET /TestAuth/Login?userId=&name=` only in the Development environment and only when configuration value `E2E_TEST_AUTH` is true. The endpoint SHALL sign in a user whose `buid` claim equals `userId` and whose name equals `name`, then redirect to `/`. It SHALL NOT create a user record and SHALL NOT validate `userId`.

#### Scenario: Flag disabled
- **WHEN** `E2E_TEST_AUTH` is absent or false
- **THEN** `/TestAuth/Login` is not mapped

#### Scenario: Flag set outside Development
- **WHEN** `E2E_TEST_AUTH` is true and the environment is not Development
- **THEN** `/TestAuth/Login` is not mapped
