using System.Text.Json;
using McpMsSqlServer.Services;
using McpMsSqlServer.Tools;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpMsSqlServer.Tests;

/// <summary>
/// Creates a throw-away LocalDB database with schemas dbo, sales and hr, and exercises the tools
/// with a configuration that allows only dbo and sales. When LocalDB is not reachable the tests
/// return early (there is no dynamic skip in xunit v2), so they never fail on machines without it.
/// </summary>
public sealed class LocalDbFixture : IDisposable
{
    private const string Server = @"Server=(localdb)\MSSQLLocalDB;";
    private const string Options = "Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=10;";

    public string DatabaseName { get; } = "McpSchemaTests_" + Guid.NewGuid().ToString("N");
    public bool Available { get; }
    public TestConfig? Config { get; }
    public ConfigService ConfigService => Config!.ConfigService;
    public DatabaseService Db { get; private set; } = null!;
    public SecurityService Security { get; private set; } = null!;
    public TransactionService Tx { get; private set; } = null!;

    public LocalDbFixture()
    {
        try
        {
            Exec("master", $"CREATE DATABASE [{DatabaseName}]");
            Exec(DatabaseName, "CREATE SCHEMA sales");
            Exec(DatabaseName, "CREATE SCHEMA hr");
            Exec(DatabaseName, "CREATE TABLE dbo.Customers (Id int PRIMARY KEY, Name nvarchar(50) NOT NULL)");
            Exec(DatabaseName, "CREATE TABLE sales.Orders (Id int PRIMARY KEY, CustomerId int NOT NULL REFERENCES dbo.Customers(Id), Total int NOT NULL)");
            Exec(DatabaseName, "CREATE TABLE hr.Salaries (Id int PRIMARY KEY, Amount int NOT NULL)");
            Exec(DatabaseName, "INSERT INTO dbo.Customers VALUES (1, N'Alice'), (2, N'Bob')");
            Exec(DatabaseName, "INSERT INTO sales.Orders VALUES (10, 1, 100), (11, 2, 200)");
            Exec(DatabaseName, "INSERT INTO hr.Salaries VALUES (1, 5000)");
            Available = true;
        }
        catch
        {
            Available = false;
            return;
        }

        var conn = (Server + $"Database={DatabaseName};" + Options).Replace("\\", "\\\\");
        Config = new TestConfig($$"""
        {
          "name": "integration",
          "connectionString": "{{conn}}",
          "allowedSchemas": ["dbo", "sales"],
          "permissions": { "allowRead": true, "allowWrite": true, "allowDelete": true },
          "security": { "requireWhereClause": true, "maxRowsPerQuery": 100, "maxRowsPerUpdate": 10, "maxRowsPerDelete": 10, "auditOperations": false },
          "querySettings": { "timeoutSeconds": 30, "enableQueryPlan": false },
          "restrictedTables": [],
          "allowedOperations": ["SELECT", "INSERT", "UPDATE", "DELETE"]
        }
        """);
        Db = new DatabaseService(ConfigService, NullLogger<DatabaseService>.Instance);
        Security = new SecurityService(ConfigService);
        Tx = new TransactionService(ConfigService, NullLogger<TransactionService>.Instance);
    }

    private static void Exec(string database, string sql)
    {
        using var connection = new SqlConnection(Server + $"Database={database};" + Options);
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        Config?.Dispose();
        if (!Available) return;
        try
        {
            SqlConnection.ClearAllPools();
            Exec("master", $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]");
        }
        catch
        {
            // best effort cleanup of the throw-away database
        }
    }
}

public class MultiSchemaIntegrationTests : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _f;

    public MultiSchemaIntegrationTests(LocalDbFixture fixture) => _f = fixture;

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Prop(JsonElement e, string name) =>
        e.EnumerateObject().First(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    private static bool HasProp(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.EnumerateObject().Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static void AssertError(string json, string contains)
    {
        var root = Parse(json);
        Assert.True(HasProp(root, "error"), json);
        Assert.Contains(contains, Prop(root, "error").GetString());
    }

    [Fact]
    public async Task Insert_Update_Delete_WorkInSecondAllowedSchema()
    {
        if (!_f.Available) return;

        var insert = await InsertTools.InsertRecords("Orders", "sales", """{"Id":100,"CustomerId":1,"Total":5}""", true,
            _f.Db, _f.ConfigService, _f.Security, _f.Tx);
        Assert.True(Prop(Parse(insert), "success").GetBoolean(), insert);

        var update = await UpdateTools.UpdateRecords("sales.Orders", null, """{"Total":6}""", "Id = 100", true,
            _f.Db, _f.ConfigService, _f.Security, _f.Tx);
        Assert.True(Prop(Parse(update), "success").GetBoolean(), update);

        var select = await QueryTools.ExecuteQuery("SELECT Total FROM sales.Orders WHERE Id = 100", 10, _f.Db, _f.Security, _f.ConfigService);
        Assert.Contains("6", select);

        var delete = await DeleteTools.DeleteRecords("Orders", "sales", "Id = 100", true,
            _f.Db, _f.ConfigService, _f.Security, _f.Tx);
        Assert.True(Prop(Parse(delete), "success").GetBoolean(), delete);
    }

    [Fact]
    public async Task WriteTools_RejectDisallowedSchema()
    {
        if (!_f.Available) return;

        AssertError(await InsertTools.InsertRecords("Salaries", "hr", """{"Id":2,"Amount":1}""", true,
            _f.Db, _f.ConfigService, _f.Security, _f.Tx), "schema 'hr' is not allowed");
        AssertError(await UpdateTools.UpdateRecords("hr.Salaries", null, """{"Amount":1}""", "Id = 1", true,
            _f.Db, _f.ConfigService, _f.Security, _f.Tx), "schema 'hr' is not allowed");
        AssertError(await DeleteTools.DeleteRecords("Salaries", "hr", "Id = 1", true,
            _f.Db, _f.ConfigService, _f.Security, _f.Tx), "schema 'hr' is not allowed");
    }

    [Fact]
    public async Task GetTableInfo_ResolvesSchema()
    {
        if (!_f.Available) return;

        var ok = await TableInfoTools.GetTableInfo("Orders", "sales", 5, _f.Db, _f.ConfigService, _f.Security);
        Assert.False(HasProp(Parse(ok), "error"), ok);
        Assert.Contains("CustomerId", ok);

        AssertError(await TableInfoTools.GetTableInfo("Salaries", "hr", 5, _f.Db, _f.ConfigService, _f.Security),
            "schema 'hr' is not allowed");
    }

    [Fact]
    public async Task ExecuteQuery_EnforcesSchemasAndAllowsCrossSchemaJoin()
    {
        if (!_f.Available) return;

        AssertError(await QueryTools.ExecuteQuery("SELECT * FROM [hr].[Salaries]", 10, _f.Db, _f.Security, _f.ConfigService),
            "schema 'hr' is not allowed");

        var join = await QueryTools.ExecuteQuery(
            "SELECT c.Name, o.Total FROM dbo.Customers c JOIN sales.Orders o ON o.CustomerId = c.Id", 10,
            _f.Db, _f.Security, _f.ConfigService);
        Assert.False(HasProp(Parse(join), "error"), join);
        Assert.Contains("Alice", join);
    }

    [Fact]
    public async Task GetSchemaInfo_CoversAllAllowedSchemas_OrOne()
    {
        if (!_f.Available) return;

        var all = Parse(await SchemaTools.GetSchemaInfo(null, null, _f.Db, _f.ConfigService, _f.Security));
        var names = Prop(all, "schemas").EnumerateArray().Select(s => Prop(s, "schema").GetString()).ToArray();
        Assert.Equal(new[] { "dbo", "sales" }, names);

        var one = Parse(await SchemaTools.GetSchemaInfo(null, "sales", _f.Db, _f.ConfigService, _f.Security));
        Assert.Equal("sales", Prop(one, "schema").GetString());
        Assert.Equal(1, Prop(one, "tableCount").GetInt32());

        AssertError(await SchemaTools.GetSchemaInfo(null, "hr", _f.Db, _f.ConfigService, _f.Security), "schema 'hr' is not allowed");
    }

    [Fact]
    public async Task DiscoverData_SpansAllowedSchemasOnly()
    {
        if (!_f.Available) return;

        var json = await DataDiscoveryTool.DiscoverData("*", "table", false, null, _f.Db, _f.ConfigService, _f.Security);
        Assert.False(HasProp(Parse(json), "error"), json);
        var tables = Prop(Prop(Parse(json), "discovery"), "Tables").EnumerateArray()
            .Select(t => $"{Prop(t, "Schema").GetString()}.{Prop(t, "TableName").GetString()}").ToList();

        Assert.Contains("dbo.Customers", tables);
        Assert.Contains("sales.Orders", tables);
        Assert.DoesNotContain("hr.Salaries", tables);

        AssertError(await DataDiscoveryTool.DiscoverData("*", "table", false, "hr", _f.Db, _f.ConfigService, _f.Security),
            "schema 'hr' is not allowed");
    }

    [Fact]
    public async Task AnalyzeTableRelationships_FollowsForeignKeysAcrossSchemas()
    {
        if (!_f.Available) return;

        var json = await DataDiscoveryTool.AnalyzeTableRelationships("sales.Orders", true, true, null,
            _f.Db, _f.ConfigService, _f.Security);
        var root = Parse(json);
        Assert.True(Prop(root, "success").GetBoolean(), json);

        var referenced = Prop(Prop(root, "analysis"), "ReferencedTables").EnumerateObject().First().Value
            .EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("dbo.Customers", referenced);

        AssertError(await DataDiscoveryTool.AnalyzeTableRelationships("hr.Salaries", true, true, null,
            _f.Db, _f.ConfigService, _f.Security), "schema 'hr' is not allowed");
    }

    [Fact]
    public async Task ProfileDataQuality_ResolvesQualifiedNames()
    {
        if (!_f.Available) return;

        var json = await DataDiscoveryTool.ProfileDataQuality("sales.Orders", 100, null, _f.Db, _f.ConfigService, _f.Security);
        var profile = Prop(Parse(json), "profiles").EnumerateArray().Single();
        Assert.Equal("sales", Prop(profile, "Schema").GetString());
        Assert.Equal("Orders", Prop(profile, "TableName").GetString());
    }

    [Fact]
    public async Task BuildQuery_SuggestsCrossSchemaJoin()
    {
        if (!_f.Available) return;

        var json = await QueryBuilderTool.BuildQuery("orders per customer", "Customers,sales.Orders", "SELECT", null,
            _f.Db, _f.ConfigService, _f.Security);
        var root = Parse(json);
        Assert.True(Prop(root, "success").GetBoolean(), json);

        var joins = Prop(root, "joinSuggestions").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(joins, j => j!.Contains("JOIN dbo.Customers") && j.Contains("sales.Orders.CustomerId"));

        AssertError(await QueryBuilderTool.BuildQuery("salaries", "hr.Salaries", "SELECT", null,
            _f.Db, _f.ConfigService, _f.Security), "schema 'hr' is not allowed");
    }

    [Fact]
    public async Task PerformanceTools_AcceptSchemaFilter()
    {
        if (!_f.Available) return;

        var stats = await PerformanceAnalysisTool.GetDatabasePerformanceStats(1, false, "sales",
            _f.Db, _f.ConfigService, _f.Security);
        var statsRoot = Parse(stats);
        Assert.False(HasProp(statsRoot, "error"), stats);
        var tableSchemas = Prop(Prop(statsRoot, "performanceStats"), "TableStats").EnumerateArray()
            .Select(t => Prop(t, "Schema").GetString()).Distinct().ToList();
        Assert.Equal(new[] { "sales" }, tableSchemas);

        AssertError(await PerformanceAnalysisTool.GetDatabasePerformanceStats(1, false, "hr",
            _f.Db, _f.ConfigService, _f.Security), "schema 'hr' is not allowed");

        var analysis = await PerformanceAnalysisTool.AnalyzeQueryPerformance("SELECT * FROM sales.Orders", false, true, null,
            _f.Db, _f.ConfigService, _f.Security);
        Assert.True(Prop(Parse(analysis), "success").GetBoolean(), analysis);
    }
}
