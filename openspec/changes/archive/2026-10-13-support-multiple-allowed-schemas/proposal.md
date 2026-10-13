# Proposal

## Why

A configuration can currently expose exactly one schema through the single string `allowedSchema`. Real databases spread related tables over several schemas (e.g. `dbo` + `Trading` + `reporting`), so users must create one configuration per schema and switch between them, and cannot join or discover across schemas. Every tool also reads `config.AllowedSchema` directly, so the limitation is baked into the whole code base.

## What Changes

- Add `allowedSchemas` (array of strings) to the configuration model; it replaces the single `allowedSchema`.
- Keep backward compatibility: a legacy `allowedSchema` string in a JSON file is read and treated as a one-element `allowedSchemas` list.
- Table-oriented tools (`GetSchemaInfo`, `GetTableInfo`, insert/update/delete) accept an optional `schemaName` argument (or a `schema.table` name); it must be one of the allowed schemas, and defaults to the first allowed schema when omitted.
- Discovery/analysis tools (data discovery, query builder, performance analysis, schema info) iterate over all allowed schemas instead of one.
- `SecurityService.IsSchemaAllowed` checks membership in the list; an empty list keeps today's "no restriction" meaning.
- `GetSchemaPrefix`/`EnsureSchemaPrefix` use the default (first) schema.
- `RestrictedTables` entries may be qualified as `schema.table`; unqualified entries keep matching in every schema.
- Fix a related gap: `ExecuteQuery` currently never checks schemas at all (`ExtractTableNames` ignores the schema part). Schema-qualified tables in submitted queries are validated against the allowed list.
- Update README, CLAUDE.md and example configurations.

## Capabilities

### New Capabilities
- `schema-access-control`: which database schemas a configuration exposes, how the allowed list is configured (including legacy single-value form), and how tools resolve and enforce the schema for each operation.

### Modified Capabilities
<!-- None: openspec/specs/ is empty, so there is no existing spec to modify. -->

## Impact

- Code: `Models/DatabaseConfig.cs`, `Services/ConfigService.cs`, `Services/SecurityService.cs`, `Services/DatabaseService.cs`, and all tools in `Tools/` that reference `AllowedSchema` (Schema, TableInfo, Insert, Update, Delete, QueryBuilder, DataDiscovery, PerformanceAnalysis; `ExecuteQuery` gains schema validation).
- Configuration: `Configurations/*.json` may use `allowedSchemas`; legacy files keep working. `GetCurrentConfigurationInfo` and `TestConnectionDetailed` output report the list.
- Docs: README.md, CLAUDE.md.
- Tool API: new optional `schemaName` parameters (non-breaking); output of `GetSchemaInfo` for multiple schemas groups tables per schema.
- No new dependencies.
