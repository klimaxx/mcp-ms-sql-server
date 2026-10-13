# Design

## Context

`DatabaseConfig.AllowedSchema` (string) is read directly in ~20 places: `SecurityService.IsSchemaAllowed`, `DatabaseService.GetSchemaPrefix/TestConnectionDetailed`, `ConfigService.GetCurrentConfigurationInfo`, and Schema, TableInfo, Insert, Update, Delete, QueryBuilder, DataDiscovery and PerformanceAnalysis tools. Write tools build `[schema].[table]` from it. Observed gaps:

- `ExecuteQuery` only calls `ValidateQueryAsync`, which never consults the schema; `ExtractTableNames` captures only the first identifier, so `[dbo].[T]` yields `dbo`, which is then compared with `RestrictedTables` (a latent bug).
- `ValidateTableAccessAsync` compares the schema passed in by the tool, which is always the config value, so the check is currently tautological.
- Several helpers (`SearchTables`, `GetIndexUsageStatistics`, ...) take a single schema string and filter `= @schema`.

## Goals / Non-Goals

**Goals:**
- One authoritative resolver for "which schema applies to this call" shared by all tools.
- Backward-compatible configuration loading.
- Real schema enforcement for free-form queries.

**Non-Goals:**
- Per-schema permissions (read/write/delete per schema) — all allowed schemas share the configuration's `Permissions`.
- Cross-database access, or changing how configurations are selected.
- Replacing the regex-based SQL validation with a real parser.

## Decisions

1. **Model: `List<string> AllowedSchemas` plus legacy `AllowedSchema` setter.** `AllowedSchema` stays as a JSON-only property whose setter appends to `AllowedSchemas` if the list is empty (and is ignored when `allowedSchemas` is given). Alternative: custom `JsonConverter` — more code for the same result. A computed `DefaultSchema` returns the first entry; when the list is empty it is empty (unrestricted, empty prefix), preserving today's behavior.

2. **Central helper in `SecurityService`:** `ResolveSchema(string? schemaName, ref string tableName)` — splits `schema.table`, falls back to the default schema, validates membership, returns `(schema, error)`. Tools call it instead of reading config, so `ValidateTableAccessAsync` becomes a meaningful check. Alternative: validate in each tool — duplicates logic and was how the tautology arose.

3. **Discovery tools run the existing single-schema helpers once per allowed schema and merge, tagging each result with its schema.** Index/table statistics queries use `IN (@schema0, @schema1, ...)` with parameters because they are single set-based queries. Alternative: rewrite every helper to `IN` lists — larger diff and the per-table dependency/profile helpers recurse per schema anyway. Foreign-key traversal resolves the referenced table's own schema, and tables outside the allowed schemas are neither listed nor expanded.

4. **Query validation:** replace `ExtractTableNames` with a pattern capturing optional `[schema].` / `schema.` before the table (handling brackets), returning `(schema?, table)`. Qualified schemas are checked against the allowed list; restricted-table matching uses the table part (and `schema.table` entries when a schema is present). Unqualified names are allowed — SQL Server resolves them by the login's default schema, which can't be fully known client-side. Accepted limitation, documented.

5. **Restricted tables:** an entry containing `.` is compared to `schema.table`; otherwise to the table name only. Behavior for existing configs is unchanged.

6. **Output shape:** `GetSchemaInfo` without a table returns `schemas: [{ schema, tableCount, tables }]`; the single-schema case keeps the existing top-level `schema`/`tables` fields additionally to avoid breaking clients.

## Risks / Trade-offs

- [Unqualified table in free-form query hits a non-allowed schema via the login's default schema] → documented; users who need strictness should grant the SQL login only the allowed schemas. Not a regression (not enforced today).
- [Output shape change for discovery tools] → keep existing fields where a single schema is configured.
- [Default schema when `allowedSchemas` is empty] → table tools fall back to `dbo` (previously produced an invalid `[].[table]` name); free-form queries stay unrestricted as today.
- [No test project] → add a small xUnit project for the pure logic (config loading, resolver, table extraction) as part of this change.
