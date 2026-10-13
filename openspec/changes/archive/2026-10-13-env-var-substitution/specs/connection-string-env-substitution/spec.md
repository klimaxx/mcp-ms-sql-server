# Spec Delta

## Purpose

Lets a configuration reference secrets such as database passwords by environment variable name in its connection string, so credentials do not have to be stored in configuration files that are committed to source control.

## ADDED Requirements

### Requirement: Placeholder expansion in connection strings
A configuration's `connectionString` SHALL have every `${NAME}` placeholder replaced with the value of the process environment variable `NAME`. `NAME` SHALL start with a letter or underscore and contain only letters, digits and underscores. All occurrences SHALL be replaced, and text outside placeholders SHALL be left unchanged.

#### Scenario: Single placeholder
- **WHEN** the connection string is `Server=db;Password=${DB_PASSWORD};` and `DB_PASSWORD` is `s3cret`
- **THEN** connections are opened with `Server=db;Password=s3cret;`

#### Scenario: Multiple placeholders
- **WHEN** the connection string contains `${DB_USER}` and `${DB_PASSWORD}` and both variables are set
- **THEN** both are replaced by their values

#### Scenario: No placeholders
- **WHEN** the connection string contains no `${...}` text
- **THEN** it is used exactly as written

#### Scenario: Value that itself looks like a placeholder
- **WHEN** `DB_PASSWORD` is set to `${OTHER}`
- **THEN** the password is used literally as `${OTHER}` and `OTHER` is not looked up

#### Scenario: Not a valid placeholder
- **WHEN** the connection string contains `${}` or `${1A}` or `$NAME`
- **THEN** that text is left unchanged

### Requirement: Unresolved placeholders block connections
If a connection string references a variable that is not set, the configuration SHALL remain loaded and listed, but any attempt to connect with it SHALL fail with an error that names every missing variable and the configuration. The server SHALL NOT connect using the partially expanded or literal placeholder text, and SHALL NOT fall back to another configuration. An environment variable that is set to an empty string SHALL count as set.

#### Scenario: Missing variable
- **WHEN** the active configuration references `${DB_PASSWORD}` and `DB_PASSWORD` is not set
- **THEN** a tool that needs the database returns an error naming `DB_PASSWORD` and the configuration, and no connection attempt is made

#### Scenario: Other configurations unaffected
- **WHEN** one configuration has an unresolved placeholder and another does not
- **THEN** the other configuration loads and works normally, and the first still appears in the list of configurations

#### Scenario: Several missing variables
- **WHEN** two referenced variables are not set
- **THEN** the error names both

#### Scenario: Only some variables missing
- **WHEN** only one of two referenced variables is missing
- **THEN** the error names only the missing one

### Requirement: Secret values are not disclosed
Resolved variable values SHALL NOT appear in tool output, error messages, or the current-configuration report. Messages about unresolved placeholders SHALL contain variable names only.

#### Scenario: Current configuration report
- **WHEN** the current configuration is reported for a connection string that used `${DB_PASSWORD}`
- **THEN** the report does not contain the password value or the connection string

#### Scenario: Connection failure message
- **WHEN** a connection fails because of a bad password that came from `${DB_PASSWORD}`
- **THEN** the returned error does not include the resolved connection string
