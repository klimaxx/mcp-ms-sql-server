# Tasks

## 1. Configuration model and test harness

- [x] 1.1 Add `AllowedSchemas` list, legacy `AllowedSchema` input handling and `DefaultSchema` to `Models/DatabaseConfig.cs`; verify with unit tests that legacy-only, list-only, both, and empty JSON load as specified
- [x] 1.2 Create an xUnit test project referencing `McpMsSqlServer` and verify `dotnet test` runs from the repo
- [x] 1.3 Update `ConfigService` default config, `GetCurrentConfigurationInfo` and `DatabaseService.TestConnectionDetailedAsync` to report `allowedSchemas`; verify via unit test of the info JSON

## 2. Security service

- [x] 2.1 Make `IsSchemaAllowed` check list membership (empty list = unrestricted); verify with unit tests
- [x] 2.2 Add `ResolveSchema` handling `schemaName` arg, `schema.table` names and default fallback; verify with unit tests for allowed, omitted and rejected schemas
- [x] 2.3 Rewrite table-name extraction to capture optional schema (bracketed or plain), validate schemas in `ValidateQuery`, and support `schema.table` entries in `RestrictedTables`; verify with unit tests incl. `[hr].[Salaries]`, cross-schema join and qualified/unqualified restrictions
- [x] 2.4 Update `GetSchemaPrefix`/`EnsureSchemaPrefix` to use `DefaultSchema`; verify with unit test

## 3. Table-oriented tools

- [x] 3.1 Add optional `schemaName` to `GetTableInfo`, `InsertRecords`, `UpdateRecords`, `DeleteRecords` and resolve via `ResolveSchema`; verify against a dev database that a second schema works and a disallowed one returns the error
- [x] 3.2 Confirm `ExecuteQuery` rejects disallowed qualified schemas end-to-end; verify with a manual call against a dev database

## 4. Discovery and analysis tools

- [x] 4.1 Change `GetSchemaInfo` to cover all allowed schemas (optional `schemaName`, per-schema grouping, legacy fields kept for a single schema); verify with a two-schema configuration
- [x] 4.2 Convert `DataDiscoveryTool` helpers to schema lists (`IN` with parameters) and tag results with the schema; verify discovery output includes tables from both schemas
- [x] 4.3 Convert `QueryBuilderTool` and `PerformanceAnalysisTool` helpers likewise; verify join suggestions and index stats span schemas

## 5. Docs and configurations

- [x] 5.1 Update README.md and CLAUDE.md for `allowedSchemas`, the legacy form, the unqualified-name limitation and qualified `restrictedTables`; verify no remaining references to a single-only `allowedSchema`
- [x] 5.2 Update example configurations (e.g. `project1.json`) to `allowedSchemas` and verify `dotnet build` and `dotnet test` pass
