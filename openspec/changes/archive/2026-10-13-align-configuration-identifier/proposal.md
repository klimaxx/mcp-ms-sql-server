# Proposal

## Why

A configuration is selected by its file name (`SwitchConfiguration`, `MCP_CONFIG_NAME`, `ListConfigurations`), but `GetCurrentConfiguration` and `TestConnection` report the `name` field from inside the JSON. The two usually differ (`YMS-TIS-S10116-TIS-PROD` vs `yms-tis-dev`), and `name` is not unique (two shipped configurations are both called `trading`), so a client cannot take what the server reports and pass it back to switch configurations.

## What Changes

- `GetCurrentConfiguration` and `TestConnection` additionally report the configuration's identifier, the file name without extension, in a new `Configuration` field. The existing `Name` / `ConfigName` fields keep their values.
- `ListConfigurations` keeps its `configurations` array of identifiers and additionally returns each identifier together with its display `name`.
- `SwitchConfiguration` (and therefore `MCP_CONFIG_NAME`) matches identifiers case-insensitively when there is no exact match, and reports the canonical identifier it switched to.
- The `name` field is documented as a display label that need not be unique and is never used for selection.

Out of scope: selecting by `name`, renaming or removing `name`, and the silent fallback to `default` for unknown names.

## Capabilities

### New Capabilities
- `configuration-identity`: how a configuration is identified, selected and reported to MCP clients (identifier vs display name).

### Modified Capabilities
<!-- None: no existing spec covers configuration selection. -->

## Impact

- `McpMsSqlServer/Services/ConfigService.cs`: case-insensitive resolution, identifier in configuration info, list with names.
- `McpMsSqlServer/Services/DatabaseService.cs`: `Configuration` field in `TestConnection` output.
- `McpMsSqlServer/Tools/ConfigurationTools.cs`: list output shape.
- `McpMsSqlServer.Tests/`: new unit tests.
- `README.md`, `CLAUDE.md`: describe identifier vs display name.
- Output changes are additive; no existing field is removed or changes value.
