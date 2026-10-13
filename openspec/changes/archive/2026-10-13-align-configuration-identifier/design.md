# Design

## Context

- `ConfigService` stores configurations in a case-sensitive `Dictionary` keyed by file name; `_currentConfigName` holds the active key. `DatabaseConfig.Name` is the JSON `name` property.
- `GetCurrentConfigurationInfo()` emits `Name`, and `DatabaseService.TestConnectionDetailedAsync()` emits `ConfigName` (both `config.Name`). The env-var error message from the previous change already uses the key.
- Output property names are PascalCase (`Name`, `AllowedSchemas`), except `ListConfigurations`, which emits `configurations`.
- Observed in shipped configs: `MagicSoft-MGP-localhost-mgp.json` and `trading.json` both declare `"name": "trading"`.

See proposal.md for motivation and scope.

## Goals / Non-Goals

**Goals:**
- Anything the server reports as a configuration identifier can be fed back into `SwitchConfiguration`.
- No existing output field changes meaning or disappears.

**Non-Goals:**
- Changing the `default` fallback in `GetConfiguration` for unknown names.
- Persisting the active configuration or changing `MCP_CONFIG_NAME` semantics beyond case-insensitive matching.

## Decisions

**1. The file-name key stays the identifier; `name` stays a label.**
It is the only value that is unique by construction and it is what clients and `MCP_CONFIG_NAME` already use.
*Alternative:* select by `name`. Rejected: duplicates exist today and it would break current client setups.

**2. Add fields, do not rename or repurpose.**
`Configuration` is added next to `Name` / `ConfigName`; `ListConfigurations` keeps `configurations` (string array) and gains `details` (array of `{ configuration, name }`).
*Alternative:* turn `ConfigName` into the identifier. Rejected: silently changes a value existing clients may display or match on.

**3. Expose the active identifier through `ConfigService`.**
A `CurrentConfigurationId` property replaces direct use of `_currentConfigName` for reporting, so `DatabaseService` and the info report read the same source.

**4. Resolve names in one private method.**
`ResolveConfigurationId(string)` returns the exact match, else the single case-insensitive match, else null / ambiguous. `SwitchConfiguration` is its only user, so `MCP_CONFIG_NAME` (which calls `SwitchConfiguration`) gets the same behavior.
*Alternative:* make the dictionary `StringComparer.OrdinalIgnoreCase`. Rejected: two files differing only in case (possible on Linux) would overwrite each other at load, and the ambiguity could not be reported.

## Risks / Trade-offs

- [Clients that parse the `SwitchConfiguration` result string] → the success text stays `Switched to configuration: <id>`; only the id may differ in case from the input.
- [Case-insensitive matching changes which input is accepted] → only inputs that previously failed now succeed; exact matches behave as before.
- [`name` and identifier still look alike in output] → fields are named `Configuration` vs `Name`, and the README states which one to pass to `SwitchConfiguration`.
