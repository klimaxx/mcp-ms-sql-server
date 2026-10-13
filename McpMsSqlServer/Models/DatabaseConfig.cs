using System.Text.Json.Serialization;

namespace McpMsSqlServer.Models;

public class DatabaseConfig
{
    private List<string> _allowedSchemas = new();

    public string Name { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Schemas exposed by this configuration. Empty means no schema restriction.
    /// </summary>
    public List<string> AllowedSchemas
    {
        get => _allowedSchemas;
        set => _allowedSchemas = (value ?? new())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();
    }

    /// <summary>
    /// Legacy single-schema form. Used only when <see cref="AllowedSchemas"/> is empty,
    /// so <c>allowedSchemas</c> wins regardless of property order in the JSON file.
    /// </summary>
    public string AllowedSchema
    {
        get => DefaultSchema;
        set
        {
            if (_allowedSchemas.Count == 0 && !string.IsNullOrWhiteSpace(value))
            {
                _allowedSchemas.Add(value.Trim());
            }
        }
    }

    /// <summary>First allowed schema, or empty when unrestricted.</summary>
    [JsonIgnore]
    public string DefaultSchema => _allowedSchemas.Count > 0 ? _allowedSchemas[0] : string.Empty;
    public PermissionSettings Permissions { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
    public QuerySettings QuerySettings { get; set; } = new();
    public List<string> RestrictedTables { get; set; } = new();
    public List<string> AllowedOperations { get; set; } = new();
}

public class PermissionSettings
{
    public bool AllowRead { get; set; } = true;
    public bool AllowWrite { get; set; } = false;
    public bool AllowDelete { get; set; } = false;
    public bool AllowSchemaChanges { get; set; } = false;
}

public class SecuritySettings
{
    public bool RequireWhereClause { get; set; } = true;
    public int MaxRowsPerQuery { get; set; } = 1000;
    public int MaxRowsPerUpdate { get; set; } = 100;
    public int MaxRowsPerDelete { get; set; } = 10;
    public bool AuditOperations { get; set; } = true;
    public bool BackupRecommendations { get; set; } = true;
}

public class QuerySettings
{
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableQueryPlan { get; set; } = false;
    public bool AllowJoins { get; set; } = true;
    public bool AllowSubqueries { get; set; } = true;
}