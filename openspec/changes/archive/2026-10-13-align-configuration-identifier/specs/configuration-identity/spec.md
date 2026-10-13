# Spec Delta

## Purpose

Defines how a configuration is identified and selected by MCP clients and how the server reports it back, so that the identifier it shows can always be used to select that configuration again.

## ADDED Requirements

### Requirement: Configuration identifier
Each configuration SHALL be identified by the name of its file in the configurations directory without the `.json` extension. The `name` property inside the file SHALL be a display label only: it need not be unique and SHALL NOT be used to select a configuration.

#### Scenario: Identifier is the file name
- **WHEN** the file `YMS-TIS-S10116-TIS-PROD.json` contains `"name": "yms-tis-dev"`
- **THEN** the configuration's identifier is `YMS-TIS-S10116-TIS-PROD`

#### Scenario: Display name is not a selector
- **WHEN** `SwitchConfiguration` is called with `yms-tis-dev`
- **THEN** it reports that the configuration was not found

#### Scenario: Duplicate display names
- **WHEN** two files both declare `"name": "trading"`
- **THEN** both load and each is selectable by its own file-name identifier

### Requirement: Identifier is reported with the display name
`GetCurrentConfiguration` and `TestConnection` SHALL report the identifier of the active configuration in a `Configuration` field, in addition to the existing display-name field. `ListConfigurations` SHALL keep returning the identifiers in `configurations` and SHALL also return each identifier with its display name.

#### Scenario: Current configuration report
- **WHEN** the active configuration has identifier `trading` and display name `Trading Application`
- **THEN** `GetCurrentConfiguration` returns `Configuration: "trading"` and `Name: "Trading Application"`

#### Scenario: Reported identifier can be switched to
- **WHEN** a client passes the `Configuration` value from `GetCurrentConfiguration` to `SwitchConfiguration`
- **THEN** the switch succeeds and that configuration is active

#### Scenario: Listing
- **WHEN** `ListConfigurations` is called
- **THEN** `configurations` contains every identifier, and each identifier also appears with its display name

#### Scenario: Connection test
- **WHEN** `TestConnection` runs for the active configuration, successfully or not
- **THEN** its result contains the `Configuration` identifier

### Requirement: Case-insensitive selection
`SwitchConfiguration` and the initial `MCP_CONFIG_NAME` SHALL match an identifier exactly first and otherwise case-insensitively when exactly one identifier matches. The result SHALL name the canonical identifier that became active.

#### Scenario: Different case
- **WHEN** `SwitchConfiguration` is called with `yms-tis-s10116-tis-prod` and the identifier is `YMS-TIS-S10116-TIS-PROD`
- **THEN** that configuration becomes active and the result names `YMS-TIS-S10116-TIS-PROD`

#### Scenario: Exact match wins
- **WHEN** identifiers `Trading` and `trading` both exist and `trading` is requested
- **THEN** `trading` becomes active

#### Scenario: Ambiguous case-insensitive match
- **WHEN** identifiers `Trading` and `trading` both exist and `TRADING` is requested
- **THEN** the active configuration is unchanged and the result states that the name is ambiguous

#### Scenario: Unknown identifier
- **WHEN** no identifier matches
- **THEN** the active configuration is unchanged and the result lists the available identifiers
