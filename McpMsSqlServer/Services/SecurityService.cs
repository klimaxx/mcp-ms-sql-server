using System.Text.RegularExpressions;
using McpMsSqlServer.Models;

namespace McpMsSqlServer.Services;

public class SecurityService
{
    private readonly ConfigService _configService;
    private readonly List<string> _dangerousPatterns = new()
    {
        @";\s*(DROP|CREATE|ALTER|TRUNCATE|EXEC|EXECUTE)\s",
        @"--.*$",
        @"/\*.*?\*/",
        @"xp_cmdshell",
        @"sp_executesql",
        @"OPENROWSET",
        @"OPENDATASOURCE",
        @"OPENQUERY"
    };
    
    public SecurityService(ConfigService configService)
    {
        _configService = configService;
    }
    
    public (bool IsValid, string? Error) ValidateQuery(string query, string operationType)
    {
        var config = _configService.CurrentConfig;
        
        if (!config.AllowedOperations.Contains(operationType, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"Operation '{operationType}' is not allowed in current configuration");
        }
        
        foreach (var pattern in _dangerousPatterns)
        {
            if (Regex.IsMatch(query, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                return (false, "Query contains potentially dangerous SQL patterns");
            }
        }
        
        if (operationType.Equals("UPDATE", StringComparison.OrdinalIgnoreCase) ||
            operationType.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            if (config.Security.RequireWhereClause && !ContainsWhereClause(query))
            {
                return (false, $"{operationType} operations require a WHERE clause for safety");
            }
        }
        
        var tablesInQuery = ExtractTableNames(query);
        foreach (var (schema, table) in tablesInQuery)
        {
            if (schema != null && !IsSchemaAllowed(schema))
            {
                return (false, $"Access to schema '{schema}' is not allowed");
            }

            if (IsTableRestricted(schema, table))
            {
                var display = schema != null ? $"{schema}.{table}" : table;
                return (false, $"Access to table '{display}' is restricted");
            }
        }
        
        return (true, null);
    }
    
    public string SanitizeIdentifier(string identifier)
    {
        return Regex.Replace(identifier, @"[^\w\d_]", "");
    }
    
    public bool IsSchemaAllowed(string schema)
    {
        var allowedSchemas = _configService.CurrentConfig.AllowedSchemas;
        if (allowedSchemas.Count == 0)
            return true;

        return allowedSchemas.Any(s => s.Equals(schema, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves the schema and table for a single-table operation. The schema comes from the
    /// explicit <paramref name="schemaName"/>, a <c>schema.table</c> qualified name, or the
    /// configuration's default schema (falling back to <c>dbo</c> when unrestricted).
    /// </summary>
    public (bool IsValid, string Schema, string Table, string? Error) ResolveSchema(string? schemaName, string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return (false, string.Empty, string.Empty, "Table name cannot be empty");

        var parts = SplitIdentifier(tableName);
        if (parts.Count > 2)
            return (false, string.Empty, string.Empty, $"Invalid table name '{tableName}'");

        var table = parts[^1];
        var qualifiedSchema = parts.Count == 2 ? parts[0] : null;
        var explicitSchema = string.IsNullOrWhiteSpace(schemaName) ? null : StripBrackets(schemaName.Trim());

        if (explicitSchema != null && qualifiedSchema != null &&
            !explicitSchema.Equals(qualifiedSchema, StringComparison.OrdinalIgnoreCase))
        {
            return (false, string.Empty, string.Empty,
                $"Schema '{explicitSchema}' conflicts with schema '{qualifiedSchema}' in table name '{tableName}'");
        }

        var schema = explicitSchema ?? qualifiedSchema ?? _configService.CurrentConfig.DefaultSchema;
        if (string.IsNullOrEmpty(schema))
            schema = "dbo";

        if (!IsSafeIdentifier(schema) || !IsSafeIdentifier(table))
            return (false, string.Empty, string.Empty, $"Invalid identifier in '{tableName}'");

        if (!IsSchemaAllowed(schema))
            return (false, string.Empty, string.Empty, $"Access to schema '{schema}' is not allowed");

        return (true, schema, table, null);
    }

    /// <summary>
    /// Schemas a discovery tool should cover: the requested one (if allowed), otherwise all allowed ones.
    /// </summary>
    public (bool IsValid, List<string> Schemas, string? Error) ResolveSchemas(string? schemaName)
    {
        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            var schema = StripBrackets(schemaName.Trim());
            if (!IsSafeIdentifier(schema))
                return (false, new List<string>(), $"Invalid schema name '{schemaName}'");
            if (!IsSchemaAllowed(schema))
                return (false, new List<string>(), $"Access to schema '{schema}' is not allowed");
            return (true, new List<string> { schema }, null);
        }

        var allowed = _configService.CurrentConfig.AllowedSchemas;
        return (true, allowed.Count > 0 ? allowed.ToList() : new List<string> { "dbo" }, null);
    }

    private bool IsTableRestricted(string? schema, string table)
    {
        foreach (var entry in _configService.CurrentConfig.RestrictedTables)
        {
            var parts = SplitIdentifier(entry);
            if (parts.Count >= 2)
            {
                if (schema != null &&
                    parts[^2].Equals(schema, StringComparison.OrdinalIgnoreCase) &&
                    parts[^1].Equals(table, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (parts.Count == 1 && parts[0].Equals(table, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
    
    public async Task<(bool IsValid, string? ErrorMessage)> ValidateQueryAsync(string query)
    {
        return await Task.Run(() =>
        {
            var result = ValidateQuery(query, "SELECT");
            return (result.IsValid, result.Error);
        });
    }
    
    public async Task<(bool IsValid, string? ErrorMessage)> ValidateTableAccessAsync(string schema, string tableName)
    {
        return await Task.Run(() =>
        {
            if (!IsSchemaAllowed(schema))
            {
                return (false, $"Access to schema '{schema}' is not allowed");
            }
            
            if (IsTableRestricted(schema, tableName))
            {
                return (false, $"Access to table '{tableName}' is restricted");
            }
            
            return (true, null);
        });
    }
    
    public async Task<(bool IsValid, string? ErrorMessage)> ValidateWhereClauseAsync(string whereClause)
    {
        return await Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(whereClause))
            {
                return (false, "WHERE clause cannot be empty");
            }
            
            foreach (var pattern in _dangerousPatterns)
            {
                if (Regex.IsMatch(whereClause, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
                {
                    return (false, "WHERE clause contains potentially dangerous SQL patterns");
                }
            }
            
            return (true, null);
        });
    }
    
    private bool ContainsWhereClause(string query)
    {
        var wherePattern = @"\bWHERE\s+.+";
        return Regex.IsMatch(query, wherePattern, RegexOptions.IgnoreCase);
    }
    
    private static readonly string NamePart = @"(?:\[[^\]]+\]|\w+)";
    private static readonly string QualifiedName = $@"({NamePart}(?:\s*\.\s*{NamePart})*)";

    private List<(string? Schema, string Table)> ExtractTableNames(string query)
    {
        var tables = new List<(string? Schema, string Table)>();
        var patterns = new[]
        {
            $@"\bFROM\s+{QualifiedName}",
            $@"\bJOIN\s+{QualifiedName}",
            $@"\bUPDATE\s+{QualifiedName}",
            $@"\bINSERT\s+INTO\s+{QualifiedName}"
        };

        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(query, pattern, RegexOptions.IgnoreCase))
            {
                var parts = SplitIdentifier(match.Groups[1].Value);
                if (parts.Count == 0) continue;

                var table = parts[^1];
                var schema = parts.Count >= 2 ? parts[^2] : null;
                tables.Add((schema, table));
            }
        }

        return tables.Distinct().ToList();
    }

    private static List<string> SplitIdentifier(string value)
    {
        return Regex.Matches(value, NamePart)
            .Select(m => StripBrackets(m.Value))
            .ToList();
    }

    private static string StripBrackets(string value)
    {
        return value.Length >= 2 && value[0] == '[' && value[^1] == ']' ? value[1..^1] : value;
    }

    private static bool IsSafeIdentifier(string value)
    {
        return Regex.IsMatch(value, @"^[\w$#@ \-]+$");
    }
}
