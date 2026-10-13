# Tasks

## 1. Selection and identifier

- [x] 1.1 In `ConfigService`, add `CurrentConfigurationId` and a private resolver (exact, then single case-insensitive match, else not found or ambiguous); make `SwitchConfiguration` use it and report the canonical identifier; verify with unit tests for exact, different case, exact-wins, ambiguous and unknown inputs (leaving the active configuration unchanged on failure)
- [x] 1.2 Add tests that `MCP_CONFIG_NAME` with different case selects the configuration and that selecting by display `name` is rejected; verify the tests pass using `TestConfig` with additional configuration files

## 2. Reporting

- [x] 2.1 Add `Configuration` to the `GetCurrentConfigurationInfo()` output (keeping `Name`); verify with a test where file name and `name` differ, including that the reported value can be passed to `SwitchConfiguration`
- [x] 2.2 Add `Configuration` to both the success and error results of `DatabaseService.TestConnectionDetailedAsync()` (keeping `ConfigName`); verify with a test using an unreachable server so the error branch is exercised
- [x] 2.3 Extend `ConfigurationTools.ListConfigurations` to return `details` (`configuration` + `name`) next to the unchanged `configurations` array; verify with a test covering duplicate display names (two files named `trading`) that both identifiers are listed

## 3. Documentation

- [x] 3.1 Update `README.md` (identifier vs `name`, which value to pass to `SwitchConfiguration` / `MCP_CONFIG_NAME`, case-insensitivity, new output fields) and `CLAUDE.md`; verify `dotnet test McpMsSqlServer.Tests` and `openspec validate align-configuration-identifier` both pass
