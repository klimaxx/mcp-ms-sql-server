using System.Text.Json;
using McpMsSqlServer.Services;
using McpMsSqlServer.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpMsSqlServer.Tests;

public class ConfigurationIdentityTests
{
    private static string Config(string name, string connectionString = "Server=ok;") =>
        JsonSerializer.Serialize(new { name, connectionString });

    private static TestConfig Create(string? initialConfigName = null) => new(
        Config("Default Configuration"),
        new Dictionary<string, string>
        {
            ["YMS-TIS-S10116-TIS-PROD"] = Config("yms-tis-dev"),
            ["trading"] = Config("trading"),
            ["MagicSoft-MGP-localhost-mgp"] = Config("trading"),
        },
        initialConfigName);

    [Fact]
    public void ExactIdentifier_Switches_AndReportsIdentifier()
    {
        using var test = Create();
        var result = test.ConfigService.SwitchConfiguration("YMS-TIS-S10116-TIS-PROD");

        Assert.Equal("Switched to configuration: YMS-TIS-S10116-TIS-PROD", result);
        Assert.Equal("YMS-TIS-S10116-TIS-PROD", test.ConfigService.CurrentConfigurationId);
    }

    [Fact]
    public void DifferentCase_Switches_AndReportsCanonicalIdentifier()
    {
        using var test = Create();
        var result = test.ConfigService.SwitchConfiguration("yms-tis-s10116-tis-prod");

        Assert.Equal("Switched to configuration: YMS-TIS-S10116-TIS-PROD", result);
        Assert.Equal("YMS-TIS-S10116-TIS-PROD", test.ConfigService.CurrentConfigurationId);
    }

    [Fact]
    public void DisplayName_IsNotASelector_AndActiveConfigurationIsUnchanged()
    {
        using var test = Create();
        test.ConfigService.SwitchConfiguration("trading");

        var result = test.ConfigService.SwitchConfiguration("yms-tis-dev");

        Assert.Contains("not found", result);
        Assert.Contains("YMS-TIS-S10116-TIS-PROD", result); // lists available identifiers
        Assert.Equal("trading", test.ConfigService.CurrentConfigurationId);
    }

    [Fact]
    public void UnknownIdentifier_LeavesActiveConfigurationUnchanged()
    {
        using var test = Create();
        var result = test.ConfigService.SwitchConfiguration("nope");

        Assert.Contains("not found", result);
        Assert.Equal("default", test.ConfigService.CurrentConfigurationId);
    }

    [Fact]
    public void InitialConfigName_IsMatchedCaseInsensitively()
    {
        using var test = Create(initialConfigName: "YMS-tis-S10116-TIS-prod");
        Assert.Equal("YMS-TIS-S10116-TIS-PROD", test.ConfigService.CurrentConfigurationId);
    }

    [Fact]
    public void ExactMatchWins_AndCaseOnlyDuplicatesAreAmbiguous()
    {
        // Needs a case-sensitive file system to hold both files; Windows collapses them.
        using var test = new TestConfig(
            Config("d"),
            new Dictionary<string, string> { ["Trading"] = Config("a"), ["trading"] = Config("b") });
        if (!test.ConfigService.ListConfigurations().Contains("Trading") ||
            !test.ConfigService.ListConfigurations().Contains("trading"))
        {
            return;
        }

        Assert.Equal("Switched to configuration: trading", test.ConfigService.SwitchConfiguration("trading"));
        Assert.Equal("trading", test.ConfigService.CurrentConfigurationId);

        var result = test.ConfigService.SwitchConfiguration("TRADING");
        Assert.Contains("ambiguous", result);
        Assert.Equal("trading", test.ConfigService.CurrentConfigurationId);
    }

    [Fact]
    public void ConfigurationInfo_ReportsIdentifierAndDisplayName_AndIdentifierRoundTrips()
    {
        using var test = Create();
        test.ConfigService.SwitchConfiguration("YMS-TIS-S10116-TIS-PROD");

        using var doc = JsonDocument.Parse(test.ConfigService.GetCurrentConfigurationInfo());
        var id = doc.RootElement.GetProperty("Configuration").GetString()!;
        Assert.Equal("YMS-TIS-S10116-TIS-PROD", id);
        Assert.Equal("yms-tis-dev", doc.RootElement.GetProperty("Name").GetString());

        test.ConfigService.SwitchConfiguration("trading");
        Assert.StartsWith("Switched", test.ConfigService.SwitchConfiguration(id));
        Assert.Equal(id, test.ConfigService.CurrentConfigurationId);
    }

    [Fact]
    public void ListConfigurations_KeepsIdentifierArray_AndAddsDetailsIncludingDuplicateNames()
    {
        using var test = Create();

        using var doc = JsonDocument.Parse(ConfigurationTools.ListConfigurations(test.ConfigService));

        var ids = doc.RootElement.GetProperty("configurations").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("trading", ids);
        Assert.Contains("MagicSoft-MGP-localhost-mgp", ids);

        var details = doc.RootElement.GetProperty("details").EnumerateArray()
            .ToDictionary(e => e.GetProperty("configuration").GetString()!, e => e.GetProperty("name").GetString());
        Assert.Equal("trading", details["trading"]);
        Assert.Equal("trading", details["MagicSoft-MGP-localhost-mgp"]); // duplicate display names, distinct identifiers
        Assert.Equal("yms-tis-dev", details["YMS-TIS-S10116-TIS-PROD"]);
        Assert.Equal(ids.Count, details.Count);
    }

    [Fact]
    public async Task TestConnection_ReportsIdentifier_OnErrorBranch()
    {
        using var test = new TestConfig(
            Config("Default Configuration"),
            new Dictionary<string, string>
            {
                ["unreachable"] = Config("Some Label", "Server=127.0.0.1,1;User Id=u;Password=p;Connect Timeout=2;TrustServerCertificate=True;"),
            });
        test.ConfigService.SwitchConfiguration("unreachable");
        var db = new DatabaseService(test.ConfigService, NullLogger<DatabaseService>.Instance);

        using var doc = JsonDocument.Parse(await db.TestConnectionDetailedAsync());

        Assert.False(doc.RootElement.GetProperty("Success").GetBoolean());
        Assert.Equal("unreachable", doc.RootElement.GetProperty("Configuration").GetString());
        Assert.Equal("Some Label", doc.RootElement.GetProperty("ConfigName").GetString());
    }
}
