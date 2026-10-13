# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview
A C# (.NET 9) Model Context Protocol server that gives MCP clients (Claude Code / Claude Desktop) controlled access to Microsoft SQL Server over STDIO. Behavior is driven by per-project JSON configurations that set the connection string, allowed schemas, permissions and safety limits.

The .NET project lives in the `McpMsSqlServer/` subfolder (the repo root only holds `README.md`, `mcp-sql-server-plan.md` and this file). Tests live in the sibling `McpMsSqlServer.Tests/` project (`dotnet test McpMsSqlServer.Tests` from the repo root). Integration tests create a throw-away database on `(localdb)\MSSQLLocalDB` and return early when LocalDB is unavailable.

## Commands
Run from `McpMsSqlServer/` (tests: see above):
```bash
dotnet build                          # Debug build
dotnet build -c Release               # produces bin/Release/net9.0/McpMsSqlServer.exe (what MCP clients launch)
dotnet run                            # speaks JSON-RPC on stdin/stdout
MCP_CONFIG_NAME=project1 dotnet run   # start with a specific configuration
```
Env vars actually read by the code: `MCP_CONFIG_NAME` (initial configuration) and `MCP_CONFIG_PATH` (override the `Configurations` directory). `MCP_DEBUG` and `--test-connection` are mentioned in docs but are **not implemented**. `${NAME}` placeholders in a configuration's `connectionString` are expanded from environment variables when configurations load (connection string only, no defaults or escape syntax); see `ConfigService.GetConnectionString()`.

## Architecture
- `Program.cs` — Generic Host; registers the four services as singletons and calls `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`. Any `[McpServerToolType]` static class under `Tools/` is auto-discovered; adding a tool is just adding a `[McpServerTool]` method.
- **stdout is the JSON-RPC channel.** Logging providers are cleared on purpose and nothing may write to stdout (`Console.WriteLine`, logging, debug prints). `ILogger` calls (including audit log in `TransactionService.LogOperationAsync`) are therefore effectively no-ops.
- Tools (`Tools/*.cs`) are static classes; services are injected as extra method parameters (`DatabaseService`, `ConfigService`, `SecurityService`, `TransactionService`) after the `[Description]`-annotated MCP arguments. Tools return JSON strings and report failures as `{ "error": ... }` rather than throwing.
- `ConfigService` — loads every `Configurations/*.json` at startup (case-insensitive property names). The **configuration name is the file name**, not the `name` field. Holds the "current" configuration as singleton in-memory state; `SwitchConfiguration` changes it for all subsequent tool calls and is not persisted. Unknown names silently fall back to the `"default"` key, so keep a `default.json`. Connections must go through `GetConnectionString()` (never read `DatabaseConfig.ConnectionString` directly): it returns the placeholder-expanded string and throws, naming the variables, when a referenced environment variable is unset. The directory is resolved relative to the exe by walking up three levels out of `bin/<cfg>/net9.0`.
- `SecurityService` — regex-based validation (dangerous patterns, restricted tables, WHERE presence) and schema/table access checks against `AllowedSchemas` and `RestrictedTables`. `ResolveSchema` (single table: `schemaName` arg, `schema.table`, or default) and `ResolveSchemas` (discovery tools: all allowed or one) are the entry points tools use; never read the schema straight from config.
- `DatabaseService` / `TransactionService` — connection creation from the current configuration and a commit/rollback wrapper. Several write tools (e.g. `UpdateTools`) open their own connection and transaction instead of using `TransactionService`.
- `Models/DatabaseConfig.cs` — configuration shape (`AllowedSchemas`, `Permissions`, `Security`, `QuerySettings`, `RestrictedTables`, `AllowedOperations`). `AllowedSchemas` is a list; the legacy JSON `allowedSchema` string is accepted and becomes a one-element list (list wins if both are present). `DefaultSchema` is the first entry; empty list = unrestricted.

### Permission enforcement is split across layers
- Read/write/delete gating uses `Permissions.AllowRead/AllowWrite/AllowDelete`, checked inside each tool.
- `AllowedOperations` is only consulted through `SecurityService.ValidateQuery` (currently called for `SELECT` from `ExecuteQuery`).
- Row limits: `MaxRowsPerQuery` truncates results client-side while reading; `MaxRowsPerUpdate/Delete` are checked with a `COUNT(*)` pre-query inside the transaction.
- Write tools interpolate `[schema].[table]` from config/user input and splice the caller's `whereClause` directly into SQL (values go through `SqlParameter`s). Preserve the identifier handling and `ValidateWhereClauseAsync` checks when touching these tools.

## Configurations
`Configurations/*.json` are committed to git and contain connection strings; keep real credentials out of anything you commit (e.g. `YMS-TIS-S10116-TIS-PROD.json` is currently untracked). `README.md` documents the full schema and example dev/prod configurations.

## Package notes
`ModelContextProtocol` 0.2.0-preview.1, `Microsoft.Data.SqlClient` 6.0.2, `Microsoft.Extensions.Hosting` 9.0.5. The MCP SDK is a preview; attribute/API names may change between versions.
