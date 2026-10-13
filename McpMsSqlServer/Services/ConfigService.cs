using System.Text.Json;
using System.Text.RegularExpressions;
using McpMsSqlServer.Models;

namespace McpMsSqlServer.Services;

public class ConfigService
{
    private static readonly Regex EnvPlaceholder = new(@"\$\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

    private readonly string _configPath;
    private Dictionary<string, DatabaseConfig> _configurations = new();
    private string _currentConfigName = "default";
    
    public DatabaseConfig CurrentConfig => GetConfiguration(_currentConfigName);
    
    public ConfigService()
    {
        // Get the base directory where the executable is located
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        
        // Look for Configurations folder in the project root (going up from bin/Release/net9.0)
        var projectRoot = baseDirectory;
        if (baseDirectory.Contains("bin"))
        {
            // Navigate up to project root
            projectRoot = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", ".."));
        }
        
        _configPath = Environment.GetEnvironmentVariable("MCP_CONFIG_PATH") 
            ?? Path.Combine(projectRoot, "Configurations");
            
        LoadConfigurations();
        
        var configName = Environment.GetEnvironmentVariable("MCP_CONFIG_NAME");
        if (!string.IsNullOrEmpty(configName))
        {
            SwitchConfiguration(configName);
        }
    }
    
    private void LoadConfigurations()
    {
        if (!Directory.Exists(_configPath))
        {
            Directory.CreateDirectory(_configPath);
        }
        
        var configFiles = Directory.GetFiles(_configPath, "*.json");
        
        foreach (var file in configFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var config = JsonSerializer.Deserialize<DatabaseConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (config != null)
                {
                    var configName = Path.GetFileNameWithoutExtension(file);
                    ResolveConnectionString(config);
                    if (config.MissingEnvironmentVariables.Count > 0)
                    {
                        // Names only: resolved values must never be written anywhere.
                        Console.Error.WriteLine(
                            $"Configuration {configName}: environment variable(s) not set: " +
                            string.Join(", ", config.MissingEnvironmentVariables));
                    }
                    _configurations[configName] = config;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading configuration {file}: {ex.Message}");
            }
        }
        
        if (_configurations.Count == 0)
        {
            _configurations["default"] = CreateDefaultConfig();
        }
    }
    
    private static void ResolveConnectionString(DatabaseConfig config)
    {
        var missing = new List<string>();

        // Single pass: substituted values are not rescanned for further placeholders.
        config.ResolvedConnectionString = EnvPlaceholder.Replace(config.ConnectionString, match =>
        {
            var name = match.Groups[1].Value;
            var value = Environment.GetEnvironmentVariable(name);
            if (value == null)
            {
                if (!missing.Contains(name))
                {
                    missing.Add(name);
                }
                return match.Value;
            }
            return value;
        });
        config.MissingEnvironmentVariables = missing;
    }

    /// <summary>
    /// Connection string of the current configuration with environment placeholders expanded.
    /// Throws when a referenced variable is not set, so a literal or partially expanded
    /// string is never used to connect.
    /// </summary>
    public string GetConnectionString()
    {
        var config = CurrentConfig;
        if (config.MissingEnvironmentVariables.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configuration '{_currentConfigName}' references environment variable(s) that are not set: " +
                string.Join(", ", config.MissingEnvironmentVariables));
        }
        return config.ResolvedConnectionString ?? config.ConnectionString;
    }

    public DatabaseConfig GetConfiguration(string name)
    {
        return _configurations.TryGetValue(name, out var config) 
            ? config 
            : _configurations["default"];
    }
    
    public List<string> ListConfigurations()
    {
        return _configurations.Keys.ToList();
    }
    
    public string SwitchConfiguration(string configName)
    {
        if (_configurations.ContainsKey(configName))
        {
            _currentConfigName = configName;
            return $"Switched to configuration: {configName}";
        }
        return $"Configuration '{configName}' not found. Available: {string.Join(", ", _configurations.Keys)}";
    }
    
    public string GetCurrentConfigurationInfo()
    {
        var config = CurrentConfig;
        return JsonSerializer.Serialize(new
        {
            Name = config.Name,
            AllowedSchemas = config.AllowedSchemas,
            Permissions = config.Permissions,
            Security = new
            {
                config.Security.RequireWhereClause,
                config.Security.MaxRowsPerQuery,
                config.Security.AuditOperations
            },
            RestrictedTables = config.RestrictedTables,
            AllowedOperations = config.AllowedOperations
        }, new JsonSerializerOptions { WriteIndented = true });
    }
    
    private DatabaseConfig CreateDefaultConfig()
    {
        return new DatabaseConfig
        {
            Name = "Default Configuration",
            ConnectionString = "Server=localhost;Database=TestDB;Integrated Security=true;",
            AllowedSchemas = new List<string> { "dbo" },
            Permissions = new PermissionSettings
            {
                AllowRead = true,
                AllowWrite = false,
                AllowDelete = false,
                AllowSchemaChanges = false
            },
            Security = new SecuritySettings
            {
                RequireWhereClause = true,
                MaxRowsPerQuery = 100,
                MaxRowsPerUpdate = 10,
                MaxRowsPerDelete = 5,
                AuditOperations = true,
                BackupRecommendations = true
            },
            QuerySettings = new QuerySettings
            {
                TimeoutSeconds = 30,
                EnableQueryPlan = false,
                AllowJoins = true,
                AllowSubqueries = true
            },
            RestrictedTables = new List<string>(),
            AllowedOperations = new List<string> { "SELECT" }
        };
    }
}