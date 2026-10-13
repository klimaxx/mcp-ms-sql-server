using System.Text.Json;
using McpMsSqlServer.Models;

namespace McpMsSqlServer.Tests;

public class ConfigurationTests
{
    private static DatabaseConfig Parse(string json) =>
        JsonSerializer.Deserialize<DatabaseConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    [Fact]
    public void ListForm_LoadsAllSchemas()
    {
        var config = Parse("""{ "allowedSchemas": ["dbo", "Trading"] }""");
        Assert.Equal(new[] { "dbo", "Trading" }, config.AllowedSchemas);
        Assert.Equal("dbo", config.DefaultSchema);
    }

    [Fact]
    public void LegacyForm_BecomesSingleElementList()
    {
        var config = Parse("""{ "allowedSchema": "reporting" }""");
        Assert.Equal(new[] { "reporting" }, config.AllowedSchemas);
    }

    [Theory]
    [InlineData("""{ "allowedSchema": "legacy", "allowedSchemas": ["a", "b"] }""")]
    [InlineData("""{ "allowedSchemas": ["a", "b"], "allowedSchema": "legacy" }""")]
    public void BothForms_ListWinsRegardlessOfOrder(string json)
    {
        var config = Parse(json);
        Assert.Equal(new[] { "a", "b" }, config.AllowedSchemas);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "allowedSchemas": [] }""")]
    [InlineData("""{ "allowedSchema": "" }""")]
    public void NothingConfigured_IsEmptyAndUnrestricted(string json)
    {
        var config = Parse(json);
        Assert.Empty(config.AllowedSchemas);
        Assert.Equal(string.Empty, config.DefaultSchema);
    }

    [Fact]
    public void ConfigurationInfo_ReportsAllSchemas()
    {
        using var test = new TestConfig("""{ "name": "x", "allowedSchemas": ["dbo", "Trading"] }""");
        using var doc = JsonDocument.Parse(test.ConfigService.GetCurrentConfigurationInfo());
        var schemas = doc.RootElement.GetProperty("AllowedSchemas").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(new[] { "dbo", "Trading" }, schemas);
    }
}
