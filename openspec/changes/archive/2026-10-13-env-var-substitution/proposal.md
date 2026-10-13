# Proposal

## Why

The README and the checked-in `YMS-TIS-S10116-TIS-PROD.json` already tell users to write `Password=${DB_PASSWORD}` in `connectionString`, but the server never expands it: the literal `${...}` text is passed to SQL Server and the login fails. Without substitution, real credentials must live in configuration files that are committed to git.

## What Changes

- Expand `${NAME}` placeholders in a configuration's `connectionString` from process environment variables when the configuration is loaded.
- A placeholder whose variable is not set does not silently pass through. The configuration still loads and is listed, but any attempt to connect with it fails with an error that names the missing variable(s) and the configuration.
- Resolved values (passwords) are never written to output, error messages or `GetCurrentConfiguration` results; only variable names are reported.
- Connection strings without placeholders behave exactly as today.
- Correct the docs (`CLAUDE.md`, `README.md`) so the feature is described accurately, including its limits.

Out of scope: substitution in other configuration fields, default values (`${VAR:-x}`), an escape syntax for a literal `${`, and `MCP_DEBUG` / `--test-connection`.

## Capabilities

### New Capabilities
- `connection-string-env-substitution`: how `${ENV_VAR}` placeholders in a configuration's connection string are recognised, resolved, and reported when unresolved.

### Modified Capabilities
<!-- None: no existing spec covers connection strings. -->

## Impact

- `McpMsSqlServer/Services/ConfigService.cs`: resolve placeholders at load, expose a connection string accessor that fails for unresolved configurations.
- `McpMsSqlServer/Services/DatabaseService.cs` and `TransactionService.cs`: use that accessor instead of reading `CurrentConfig.ConnectionString` directly.
- `McpMsSqlServer.Tests/`: new unit tests (no LocalDB needed) using `TestConfig` and temporary environment variables.
- `CLAUDE.md`, `README.md`: documentation updates.
- No new dependencies and no change to the configuration JSON schema.
