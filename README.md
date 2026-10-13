# MCP Microsoft SQL Server

A configurable **Model Context Protocol (MCP) server** for Microsoft SQL Server integration with Claude Code and other MCP clients. Enables AI assistants to securely interact with SQL Server databases through project-based configurations with full read/write capabilities.

## 🌟 Features

### 🔧 **Project-Based Configuration**
- **Multiple database connections** - Switch between different projects and databases
- **Schema-specific access** - Restrict access to one or more schemas per project
- **Configurable permissions** - Fine-grained control over read/write/delete operations
- **Environment-based setup** - Different configurations for dev, staging, and production

### 🛡️ **Security & Safety**
- **Transaction management** - Automatic rollback on errors for write operations
- **Query validation** - Prevent SQL injection and validate all operations
- **WHERE clause enforcement** - Mandatory WHERE clauses for UPDATE/DELETE operations
- **Row limit restrictions** - Configurable limits to prevent accidental mass operations
- **Audit logging** - Track all database operations for accountability

### 🔍 **Database Operations**
- **Read Operations**: SELECT queries with pagination and filtering
- **Write Operations**: INSERT, UPDATE, DELETE with transaction safety
- **Schema Exploration**: Browse tables, columns, relationships, and indexes
- **Table Management**: Get metadata, statistics, and sample data
- **Configuration Management**: Switch between project configurations dynamically

### 🚀 **AI Integration**
- **Claude Desktop integration** - Seamless setup with Claude Desktop app
- **MCP protocol compliance** - Works with any MCP-compatible client
- **Natural language interface** - Interact with databases using plain English
- **Error handling** - Clear, actionable error messages for AI and humans

## 📋 Prerequisites

- **.NET 9.0 or later**
- **Microsoft SQL Server** (any supported version)
- **Claude Desktop** (for Claude integration)
- **Appropriate database permissions** for the operations you want to perform

## 🚀 Quick Start

### 1. Installation

```bash
# Clone the repository
git clone https://github.com/yourusername/mcp-ms-sql-server.git
cd mcp-ms-sql-server

# Build the project
dotnet build -c Release
```

### 2. Create Project Configuration

Create a configuration file for your project in the `Configurations/` directory:

```json
// Configurations/my-project.json
{
  "name": "My E-Commerce Project",
  "connectionString": "Server=localhost;Database=ECommerceDB;Integrated Security=true;",
  "allowedSchemas": ["dbo"],
  "permissions": {
    "allowRead": true,
    "allowWrite": true,
    "allowDelete": false
  },
  "security": {
    "requireWhereClause": true,
    "maxRowsPerQuery": 1000,
    "auditOperations": true
  }
}
```

### 3. Configure Claude Desktop

Add to your Claude Desktop configuration:

**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "sql-server": {
      "type": "stdio",
      "command": "C:\\Git\\mcp-ms-sql-server\\McpMsSqlServer\\bin\\Release\\net9.0\\McpMsSqlServer.exe",
      "env": {
        "MCP_CONFIG_NAME": "my-project"
      }
    }
  }
}
```

**Note**: First build the project with `dotnet build -c Release` to create the executable.

### 4. Start Using

In Claude Desktop, you can now ask:
- "Show me all tables in the database"
- "Insert a new customer with name 'John Doe' and email 'john@example.com'"
- "What are the top 10 products by sales?"
- "Update the price of product with ID 123 to $29.99"

## 📖 Configuration Guide

### Project Configuration Structure

```json
{
  "name": "Project Display Name",
  "connectionString": "Your SQL Server connection string",
  "allowedSchemas": ["schema_name", "other_schema"],
  "permissions": {
    "allowRead": true,
    "allowWrite": true,
    "allowDelete": false,
    "allowSchemaChanges": false
  },
  "security": {
    "requireWhereClause": true,
    "maxRowsPerQuery": 1000,
    "maxRowsPerUpdate": 100,
    "maxRowsPerDelete": 10,
    "auditOperations": true
  },
  "querySettings": {
    "timeoutSeconds": 30,
    "enableQueryPlan": false,
    "allowJoins": true,
    "allowSubqueries": true
  },
  "restrictedTables": ["sensitive_table", "audit_log"],
  "allowedOperations": ["SELECT", "INSERT", "UPDATE", "DELETE"]
}
```

### Allowed Schemas

`allowedSchemas` lists every schema a configuration exposes:

- The legacy single-value form `"allowedSchema": "dbo"` still works and is read as `["dbo"]`. If both are present, `allowedSchemas` wins.
- An empty or missing list means no schema restriction.
- The **first** entry is the default schema for table tools when no schema is given.
- Table tools (`GetTableInfo`, `InsertRecords`, `UpdateRecords`, `DeleteRecords`) accept an optional `schemaName` argument or a `schema.table` name. A schema outside the list is rejected.
- Discovery and analysis tools (`GetSchemaInfo`, `DiscoverData`, `BuildQuery`, `AnalyzeQueryPerformance`, `GetDatabasePerformanceStats`) cover all allowed schemas unless `schemaName` narrows them to one.
- `ExecuteQuery` rejects queries that reference a schema-qualified table in a schema that is not allowed.
- `restrictedTables` entries may be qualified (`"sales.Secrets"`, applies only to that schema) or plain (`"Secrets"`, applies to that table name in every schema).

> **Limitation:** unqualified table names in free-form queries are resolved by SQL Server using the login's default schema and cannot be checked client-side. For strict isolation, grant the SQL login access only to the allowed schemas.

### Example Configurations

<details>
<summary><strong>Development Environment</strong></summary>

```json
{
  "name": "Development Database",
  "connectionString": "Server=dev-server;Database=DevDB;Integrated Security=true;",
  "allowedSchemas": ["dbo"],
  "permissions": {
    "allowRead": true,
    "allowWrite": true,
    "allowDelete": true,
    "allowSchemaChanges": true
  },
  "security": {
    "requireWhereClause": false,
    "maxRowsPerQuery": 5000,
    "auditOperations": false
  }
}
```
</details>

<details>
<summary><strong>Production Environment</strong></summary>

```json
{
  "name": "Production Database",
  "connectionString": "Server=prod-server;Database=ProdDB;User Id=app_user;Password=secure_password;",
  "allowedSchemas": ["app"],
  "permissions": {
    "allowRead": true,
    "allowWrite": true,
    "allowDelete": false,
    "allowSchemaChanges": false
  },
  "security": {
    "requireWhereClause": true,
    "maxRowsPerQuery": 100,
    "maxRowsPerUpdate": 10,
    "auditOperations": true
  },
  "restrictedTables": ["user_passwords", "payment_info"]
}
```
</details>

## 🛠️ Available Tools

### Configuration Management (4 tools)
- **ListConfigurations** - Show all available project configurations
- **SwitchConfiguration** - Switch to a different project configuration
- **GetCurrentConfiguration** - View current configuration details
- **TestConnection** - Test database connectivity

### Core Database Operations (6 tools)
- **ExecuteQuery** - Run SELECT queries within the allowed schemas
- **GetSchemaInfo** - Explore database structure and objects
- **GetTableInfo** - Get table metadata and sample data
- **InsertRecords** - Insert new records with transaction support
- **UpdateRecords** - Update existing records (requires WHERE clause)
- **DeleteRecords** - Delete records (requires WHERE clause)

### Advanced Features (6 tools)
- **BuildQuery** - Generate SQL queries from natural language
- **AnalyzeQueryPerformance** - Analyze query execution plans
- **GetDatabasePerformanceStats** - Database performance metrics
- **DiscoverData** - Search tables/columns by patterns
- **AnalyzeTableRelationships** - Find table relationships
- **ProfileDataQuality** - Analyze data quality and statistics

## 🔒 Security Considerations

### Best Practices

- **Use dedicated database users** with minimal required permissions
- **Enable audit logging** for production environments
- **Set appropriate row limits** to prevent accidental mass operations
- **Restrict sensitive tables** using the `restrictedTables` configuration
- **Use WHERE clause requirements** for UPDATE/DELETE operations
- **Regular security reviews** of configurations and permissions

### Connection String Security

```bash
# Use environment variables for sensitive data
export DB_PASSWORD="your_secure_password"
```

```json
{
  "connectionString": "Server=myserver;Database=mydb;User Id=myuser;Password=${DB_PASSWORD};"
}
```

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### Development Setup

```bash
# Clone the repo
git clone https://github.com/yourusername/mcp-ms-sql-server.git
cd mcp-ms-sql-server

# Install dependencies
dotnet restore

# Run tests
dotnet test

# Build and test
dotnet build -c Release
```

## 📚 Documentation

- [Configuration Guide](docs/configuration-guide.md)
- [Claude Desktop Setup](docs/claude-desktop-setup.md)
- [Security Best Practices](docs/security-guide.md)
- [API Reference](docs/api-reference.md)
- [Troubleshooting](docs/troubleshooting.md)

## 🐛 Troubleshooting

### Common Issues

**Connection Failed**
```bash
# Test your connection string
dotnet run -- --test-connection --config your-project
```

**Permission Denied**
- Check your database user permissions
- Verify the `allowedSchemas` configuration
- Ensure the user has access to the specified schemas

**Configuration Not Found**
- Verify the configuration file exists in `Configurations/`
- Check the `MCP_CONFIG_NAME` environment variable
- Ensure the JSON syntax is valid

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🌟 Acknowledgments

- [Model Context Protocol](https://modelcontextprotocol.io/) by Anthropic
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) by Microsoft and Anthropic
- The open-source community for inspiration and feedback

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/mcp-ms-sql-server/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/mcp-ms-sql-server/discussions)
- **Documentation**: [Wiki](https://github.com/yourusername/mcp-ms-sql-server/wiki)

---

**Made with ❤️ for the MCP and AI development community**
