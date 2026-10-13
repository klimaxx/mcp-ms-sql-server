namespace McpMsSqlServer.Tests;

public class SecurityServiceTests
{
    private const string Config = """
    {
      "name": "t",
      "allowedSchemas": ["dbo", "Trading"],
      "restrictedTables": ["Secrets", "Trading.Tokens"],
      "allowedOperations": ["SELECT", "UPDATE"]
    }
    """;

    [Fact]
    public void IsSchemaAllowed_ChecksMembership_CaseInsensitive()
    {
        using var t = new TestConfig(Config);
        Assert.True(t.Security.IsSchemaAllowed("dbo"));
        Assert.True(t.Security.IsSchemaAllowed("trading"));
        Assert.False(t.Security.IsSchemaAllowed("hr"));
    }

    [Fact]
    public void IsSchemaAllowed_EmptyList_IsUnrestricted()
    {
        using var t = new TestConfig("""{ "name": "t" }""");
        Assert.True(t.Security.IsSchemaAllowed("anything"));
    }

    [Fact]
    public void ResolveSchema_ExplicitAllowed()
    {
        using var t = new TestConfig(Config);
        var r = t.Security.ResolveSchema("Trading", "Orders");
        Assert.True(r.IsValid);
        Assert.Equal(("Trading", "Orders"), (r.Schema, r.Table));
    }

    [Fact]
    public void ResolveSchema_Omitted_UsesFirstAllowed()
    {
        using var t = new TestConfig(Config);
        var r = t.Security.ResolveSchema(null, "Orders");
        Assert.True(r.IsValid);
        Assert.Equal("dbo", r.Schema);
    }

    [Fact]
    public void ResolveSchema_QualifiedName()
    {
        using var t = new TestConfig(Config);
        var r = t.Security.ResolveSchema(null, "[Trading].[Orders]");
        Assert.True(r.IsValid);
        Assert.Equal(("Trading", "Orders"), (r.Schema, r.Table));
    }

    [Fact]
    public void ResolveSchema_NotAllowed_ReturnsError()
    {
        using var t = new TestConfig(Config);
        var r = t.Security.ResolveSchema("sys", "objects");
        Assert.False(r.IsValid);
        Assert.Contains("'sys' is not allowed", r.Error);
    }

    [Fact]
    public void ResolveSchema_ConflictingSchemas_ReturnsError()
    {
        using var t = new TestConfig(Config);
        Assert.False(t.Security.ResolveSchema("dbo", "Trading.Orders").IsValid);
    }

    [Theory]
    [InlineData("x]; DROP TABLE y;--")]
    [InlineData("a.b.c")]
    public void ResolveSchema_RejectsUnsafeNames(string table)
    {
        using var t = new TestConfig(Config);
        Assert.False(t.Security.ResolveSchema(null, table).IsValid);
    }

    [Fact]
    public void ResolveSchema_Unrestricted_FallsBackToDbo_AndRejectsUnsafeSchema()
    {
        using var t = new TestConfig("""{ "name": "t" }""");
        Assert.Equal("dbo", t.Security.ResolveSchema(null, "Orders").Schema);
        Assert.False(t.Security.ResolveSchema("a]b", "Orders").IsValid);
    }

    [Fact]
    public void ResolveSchemas_AllOrNarrowed()
    {
        using var t = new TestConfig(Config);
        Assert.Equal(new[] { "dbo", "Trading" }, t.Security.ResolveSchemas(null).Schemas);
        Assert.Equal(new[] { "Trading" }, t.Security.ResolveSchemas("Trading").Schemas);
        Assert.False(t.Security.ResolveSchemas("hr").IsValid);
    }

    [Fact]
    public void ValidateQuery_RejectsDisallowedQualifiedSchema()
    {
        using var t = new TestConfig(Config);
        var r = t.Security.ValidateQuery("SELECT * FROM [hr].[Salaries]", "SELECT");
        Assert.False(r.IsValid);
        Assert.Contains("schema 'hr'", r.Error);
    }

    [Fact]
    public void ValidateQuery_AllowsJoinAcrossAllowedSchemas()
    {
        using var t = new TestConfig(Config);
        var r = t.Security.ValidateQuery(
            "SELECT * FROM dbo.Orders o JOIN Trading.Trades t ON t.OrderId = o.Id", "SELECT");
        Assert.True(r.IsValid, r.Error);
    }

    [Fact]
    public void ValidateQuery_AllowsUnqualifiedTables()
    {
        using var t = new TestConfig(Config);
        Assert.True(t.Security.ValidateQuery("SELECT * FROM Orders", "SELECT").IsValid);
    }

    [Fact]
    public void ValidateQuery_UnqualifiedRestriction_AppliesInEverySchema()
    {
        using var t = new TestConfig(Config);
        Assert.False(t.Security.ValidateQuery("SELECT * FROM Trading.Secrets", "SELECT").IsValid);
        Assert.False(t.Security.ValidateQuery("SELECT * FROM Secrets", "SELECT").IsValid);
    }

    [Fact]
    public void ValidateQuery_QualifiedRestriction_AppliesOnlyToThatSchema()
    {
        using var t = new TestConfig(Config);
        Assert.False(t.Security.ValidateQuery("SELECT * FROM Trading.Tokens", "SELECT").IsValid);
        Assert.True(t.Security.ValidateQuery("SELECT * FROM dbo.Tokens", "SELECT").IsValid);
    }

    [Fact]
    public async Task ValidateTableAccess_HonoursQualifiedRestriction()
    {
        using var t = new TestConfig(Config);
        Assert.False((await t.Security.ValidateTableAccessAsync("Trading", "Tokens")).IsValid);
        Assert.True((await t.Security.ValidateTableAccessAsync("dbo", "Tokens")).IsValid);
        Assert.False((await t.Security.ValidateTableAccessAsync("dbo", "Secrets")).IsValid);
        Assert.False((await t.Security.ValidateTableAccessAsync("hr", "Anything")).IsValid);
    }
}
