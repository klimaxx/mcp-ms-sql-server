# schema-access-control Specification

## Purpose
Defines which database schemas a configuration exposes to MCP clients, how the allowed list is configured (including the legacy single-schema form), and how tools resolve and enforce the schema for each operation.

## Requirements

### Requirement: Configurable list of allowed schemas
A configuration SHALL declare the schemas it exposes as a list named `allowedSchemas`. A legacy single-value `allowedSchema` SHALL be accepted and treated as a list containing that one schema. When both are present, `allowedSchemas` SHALL take precedence. An empty or missing list SHALL mean no schema restriction.

#### Scenario: Multiple schemas configured
- **WHEN** a configuration contains `"allowedSchemas": ["dbo", "Trading"]`
- **THEN** the server treats both `dbo` and `Trading` as allowed

#### Scenario: Legacy single schema
- **WHEN** a configuration contains only `"allowedSchema": "reporting"`
- **THEN** the server behaves as if `allowedSchemas` were `["reporting"]`

#### Scenario: Both properties present
- **WHEN** a configuration contains `allowedSchema` and `allowedSchemas`
- **THEN** only the schemas in `allowedSchemas` are allowed

#### Scenario: No schemas configured
- **WHEN** neither property is set or the list is empty
- **THEN** schema access is not restricted

### Requirement: Schema resolution for table operations
Tools that operate on a single table SHALL accept the schema either as an optional `schemaName` argument or as a `schema.table` qualified name. If no schema is given, the first entry of `allowedSchemas` SHALL be used. A schema outside the allowed list SHALL be rejected with an error and no database access SHALL occur.

#### Scenario: Explicit schema allowed
- **WHEN** a table tool is called with `schemaName` `Trading` and `Trading` is allowed
- **THEN** the operation runs against `[Trading].[<table>]`

#### Scenario: Schema omitted
- **WHEN** a table tool is called without a schema and `allowedSchemas` is `["dbo", "Trading"]`
- **THEN** the operation runs against `[dbo].[<table>]`

#### Scenario: Schema not allowed
- **WHEN** a table tool is called with schema `sys` and `sys` is not in `allowedSchemas`
- **THEN** the tool returns an error stating access to schema `sys` is not allowed

### Requirement: Discovery across all allowed schemas
Tools that list or analyse tables without a specific table (schema info, data discovery, query building, performance analysis) SHALL cover every allowed schema, and results SHALL identify the schema each item belongs to. An optional `schemaName` argument SHALL narrow them to one allowed schema.

#### Scenario: Listing tables
- **WHEN** schema info is requested without a table name and two schemas are allowed
- **THEN** the result contains the tables and views of both schemas, each tagged with its schema

#### Scenario: Narrowing to one schema
- **WHEN** schema info is requested with `schemaName` set to an allowed schema
- **THEN** only that schema's objects are returned

### Requirement: Schema enforcement in free-form queries
Free-form SELECT queries SHALL be rejected when they reference a schema-qualified table whose schema is not allowed. Unqualified table names SHALL be accepted and resolved by the database.

#### Scenario: Query touching a disallowed schema
- **WHEN** a query contains `FROM [hr].[Salaries]` and `hr` is not allowed
- **THEN** the query is rejected with an error naming schema `hr` and is not executed

#### Scenario: Join across allowed schemas
- **WHEN** a query joins `dbo.Orders` with `Trading.Trades` and both schemas are allowed
- **THEN** the query passes schema validation

### Requirement: Restricted tables with schema qualification
Entries in `restrictedTables` MAY be written as `schema.table`. A qualified entry SHALL restrict only that table in that schema; an unqualified entry SHALL restrict a table of that name in every schema.

#### Scenario: Qualified restriction
- **WHEN** `restrictedTables` contains `Trading.Secrets` and a tool targets `dbo.Secrets`
- **THEN** access is not blocked by that entry

#### Scenario: Unqualified restriction
- **WHEN** `restrictedTables` contains `Secrets` and a tool targets `Trading.Secrets`
- **THEN** access is rejected as restricted

### Requirement: Configuration reporting
Configuration information and connection-test output SHALL report the full list of allowed schemas.

#### Scenario: Viewing current configuration
- **WHEN** the current configuration info is requested
- **THEN** the response lists all allowed schemas
