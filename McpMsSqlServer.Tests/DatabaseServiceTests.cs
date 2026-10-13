using McpMsSqlServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpMsSqlServer.Tests;

public class DatabaseServiceTests
{
    [Fact]
    public void SchemaPrefix_UsesDefaultSchema()
    {
        using var t = new TestConfig("""{ "allowedSchemas": ["Trading", "dbo"] }""");
        var db = new DatabaseService(t.ConfigService, NullLogger<DatabaseService>.Instance);
        Assert.Equal("[Trading].", db.GetSchemaPrefix());
        Assert.Equal("[Trading].[Orders]", db.EnsureSchemaPrefix("Orders"));
        Assert.Equal("dbo.Orders", db.EnsureSchemaPrefix("dbo.Orders"));
    }

    [Fact]
    public void SchemaPrefix_EmptyWhenUnrestricted()
    {
        using var t = new TestConfig("{}");
        var db = new DatabaseService(t.ConfigService, NullLogger<DatabaseService>.Instance);
        Assert.Equal("", db.GetSchemaPrefix());
    }
}
