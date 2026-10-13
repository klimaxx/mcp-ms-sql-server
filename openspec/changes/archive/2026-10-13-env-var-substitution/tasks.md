# Tasks

## 1. Placeholder resolution

- [x] 1.1 Add non-serialized members on `DatabaseConfig` for the resolved connection string and the list of missing variable names; verify `dotnet build` passes and existing configuration JSON still deserializes (existing tests pass)
- [x] 1.2 In `ConfigService`, expand `${NAME}` placeholders in `ConnectionString` right after each file is deserialized (single pass, regex from design.md, values never rescanned) and record missing names; verify with unit tests in `McpMsSqlServer.Tests` covering the single, multiple, none, value-looks-like-placeholder and invalid-placeholder scenarios (using `TestConfig` and temporary environment variables restored after each test)
- [x] 1.3 Write a stderr line at load for configurations with missing variables (names only, no values); verify through a test that the configuration is still listed by `ListConfigurations()` and that other configurations load normally

## 2. Enforcing at connection time

- [x] 2.1 Add an accessor on `ConfigService` that returns the resolved connection string of the current configuration or throws `InvalidOperationException` naming all missing variables and the configuration; verify with tests for missing, partially missing and empty-but-set variables
- [x] 2.2 Switch `DatabaseService.CreateConnection()` and `TransactionService` to the accessor and verify by search that no other code reads `ConnectionString` directly
- [x] 2.3 Check that every caller of those two paths reports the exception as `{ "error": ... }` through existing handling (add a try/catch where one is missing); verify with a test that `TestConnection` or `ExecuteQuery` on an unresolved configuration returns an error containing the variable name and no connection attempt is made
- [x] 2.4 Verify `GetCurrentConfigurationInfo()` output and the error from a failed login contain neither the resolved connection string nor the secret value (assertion in a test using a recognisable password value)

## 3. Documentation

- [x] 3.1 Update `CLAUDE.md` (remove `${ENV_VAR}` from the "not implemented" list, describe load-time resolution and the connection-only scope) and `README.md` (syntax, unset-variable behavior, restart required, quoting of values containing `;`); verify the README example matches the behavior covered by the tests
- [x] 3.2 Run `dotnet test McpMsSqlServer.Tests` and `openspec validate env-var-substitution`, and confirm both pass; manually start the server with the `YMS-TIS-S10116-TIS-PROD` configuration once with and once without `MCP_TIS_S10116_TIS_PROD_PWD` set, checking that the unset case returns the named-variable error
