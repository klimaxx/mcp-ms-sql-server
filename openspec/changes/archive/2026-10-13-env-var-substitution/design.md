# Design

## Context

- `ConfigService` deserializes every `Configurations/*.json` into `DatabaseConfig` at startup and keeps them in a dictionary. Each file is loaded inside its own try/catch, and load errors go to stderr (stdout is the JSON-RPC channel).
- `DatabaseConfig.ConnectionString` is read directly in exactly two places: `DatabaseService.CreateConnection()` and `TransactionService` (line ~20).
- Unknown configuration names silently fall back to `default`, so a configuration that fails to load can cause connections to a different database. That makes "skip configs with missing variables" unsafe here.
- `GetCurrentConfigurationInfo()` does not currently return the connection string; this must stay true.

See proposal.md for motivation and scope.

## Goals / Non-Goals

**Goals:**
- One place that expands placeholders, with one place that decides whether a connection may be opened.
- A misconfigured environment fails loudly and specifically, never by connecting somewhere unintended.

**Non-Goals:**
- Reloading environment variables at runtime. Values are read once when configurations load.
- Changing how configurations are discovered or how `SwitchConfiguration` behaves.

## Decisions

**1. Resolve at load time, keep the raw template.**
`ConfigService` expands the connection string right after deserialization and stores the result on a separate, non-serialized member of `DatabaseConfig` (resolved string plus list of missing variable names). The original `ConnectionString` property keeps the template text, so nothing that serializes config can leak the secret and the JSON model is unchanged.
*Alternative:* overwrite `ConnectionString` in place. Rejected: the secret would then sit in the property that every other part of the code can see and might serialize.
*Alternative:* resolve on every connection. Rejected: pointless repeated work, and behavior would change if the environment changes mid-session.

**2. Missing variable = configuration stays loaded but is unusable.**
Missing names are recorded, and a single accessor on `ConfigService` (for the current configuration) throws `InvalidOperationException` naming the variables and configuration. `DatabaseService.CreateConnection()` and `TransactionService` call that accessor instead of reading `ConnectionString`.
*Alternative:* leave the placeholder and let SQL Server reject the login. Rejected: the error is misleading and a partially expanded string might be sent to a server.
*Alternative:* do not load the configuration. Rejected: because of the silent fallback to `default`, `MCP_CONFIG_NAME=prod` would quietly run against the default database.

**3. Syntax: `${NAME}` only, regex `\$\{([A-Za-z_][A-Za-z0-9_]*)\}`, single pass.**
Applied with `Regex.Replace` using a match evaluator, so substituted values are never rescanned (no recursion, no injection of further placeholders). No default-value or escape syntax; both can be added later without breaking existing strings.

**4. Substitute only `connectionString`.**
Running replacement over the raw JSON text would let values containing quotes or backslashes break the JSON, and no other field has a use case. Limiting scope keeps the behavior easy to specify.

**5. Messages carry names, never values.**
Only the variable names and configuration name are written to errors and to stderr at load. The error is surfaced through the existing `{ "error": ... }` handling in tools.

## Risks / Trade-offs

- [Password contains `;` or `=`, which can alter a connection string] → Not handled by substitution; documented as the user's responsibility (quote the value in the environment, as for any connection string).
- [Exception thrown from `CreateConnection()` is not caught by some tool and crashes the call] → Verify during implementation that each caller sits inside the tools' existing try/catch; add one where missing.
- [Environment variable set after the MCP client launched the server is not seen] → Documented; requires restarting the server, which is the normal MCP client behavior.
- [A user relies on `${...}` appearing literally in a connection string] → Extremely unlikely in SQL Server connection strings; noted in docs.
