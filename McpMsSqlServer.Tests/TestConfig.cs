using McpMsSqlServer.Services;

namespace McpMsSqlServer.Tests;

/// <summary>
/// Builds a ConfigService backed by a temporary Configurations directory.
/// </summary>
public sealed class TestConfig : IDisposable
{
    private static readonly object EnvLock = new();
    private readonly string _dir;

    public ConfigService ConfigService { get; }

    public TestConfig(
        string defaultJson,
        IDictionary<string, string>? additionalConfigs = null,
        string? initialConfigName = null)
    {
        _dir = Path.Combine(Path.GetTempPath(), "mcp-sql-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "default.json"), defaultJson);
        foreach (var (name, json) in additionalConfigs ?? new Dictionary<string, string>())
        {
            File.WriteAllText(Path.Combine(_dir, name + ".json"), json);
        }

        lock (EnvLock)
        {
            var previousPath = Environment.GetEnvironmentVariable("MCP_CONFIG_PATH");
            var previousName = Environment.GetEnvironmentVariable("MCP_CONFIG_NAME");
            try
            {
                Environment.SetEnvironmentVariable("MCP_CONFIG_PATH", _dir);
                Environment.SetEnvironmentVariable("MCP_CONFIG_NAME", initialConfigName);
                ConfigService = new ConfigService();
            }
            finally
            {
                Environment.SetEnvironmentVariable("MCP_CONFIG_PATH", previousPath);
                Environment.SetEnvironmentVariable("MCP_CONFIG_NAME", previousName);
            }
        }
    }

    public SecurityService Security => new(ConfigService);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }
}
