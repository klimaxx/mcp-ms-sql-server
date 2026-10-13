using System.Text.Json;
using McpMsSqlServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpMsSqlServer.Tests;

public class EnvSubstitutionTests
{
    /// <summary>Sets environment variables for the lifetime of the scope and restores them afterwards.</summary>
    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new();

        public EnvScope(params (string Name, string? Value)[] vars)
        {
            foreach (var (name, value) in vars)
            {
                _previous[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    private static string Config(string connectionString) =>
        JsonSerializer.Serialize(new { name = "t", connectionString });

    private static string Resolve(string connectionString)
    {
        using var test = new TestConfig(Config(connectionString));
        return test.ConfigService.GetConnectionString();
    }

    [Fact]
    public void SinglePlaceholder_IsReplaced()
    {
        using var env = new EnvScope(("MCPT_SINGLE_PWD", "s3cret"));
        Assert.Equal("Server=db;Password=s3cret;", Resolve("Server=db;Password=${MCPT_SINGLE_PWD};"));
    }

    [Fact]
    public void MultiplePlaceholders_AreAllReplaced_IncludingRepeats()
    {
        using var env = new EnvScope(("MCPT_MULTI_USER", "alice"), ("MCPT_MULTI_PWD", "pw"));
        Assert.Equal(
            "User Id=alice;Password=pw;Application Name=alice;",
            Resolve("User Id=${MCPT_MULTI_USER};Password=${MCPT_MULTI_PWD};Application Name=${MCPT_MULTI_USER};"));
    }

    [Fact]
    public void NoPlaceholders_IsUnchanged()
    {
        Assert.Equal("Server=localhost;Database=x;Integrated Security=true;",
            Resolve("Server=localhost;Database=x;Integrated Security=true;"));
    }

    [Fact]
    public void ValueLookingLikePlaceholder_IsNotRescanned()
    {
        using var env = new EnvScope(("MCPT_RESCAN_PWD", "${MCPT_RESCAN_OTHER}"), ("MCPT_RESCAN_OTHER", "nope"));
        Assert.Equal("Password=${MCPT_RESCAN_OTHER};", Resolve("Password=${MCPT_RESCAN_PWD};"));
    }

    [Theory]
    [InlineData("Password=${};")]
    [InlineData("Password=${1A};")]
    [InlineData("Password=$MCPT_BARE;")]
    [InlineData("Password=${HAS SPACE};")]
    public void InvalidPlaceholder_IsLeftUnchanged(string connectionString)
    {
        Assert.Equal(connectionString, Resolve(connectionString));
    }

    [Fact]
    public void EmptyButSetVariable_CountsAsSet()
    {
        using var env = new EnvScope(("MCPT_EMPTY_PWD", ""));
        Assert.Equal("Password=;", Resolve("Password=${MCPT_EMPTY_PWD};"));
    }

    [Fact]
    public void MissingVariable_ThrowsNamingVariableAndConfiguration_ButConfigurationStaysListed()
    {
        using var env = new EnvScope(("MCPT_MISSING_PWD", null));
        using var test = new TestConfig(Config("Password=${MCPT_MISSING_PWD};"));

        Assert.Contains("default", test.ConfigService.ListConfigurations());
        var ex = Assert.Throws<InvalidOperationException>(() => test.ConfigService.GetConnectionString());
        Assert.Contains("MCPT_MISSING_PWD", ex.Message);
        Assert.Contains("'default'", ex.Message);
    }

    [Fact]
    public void SeveralMissing_AreAllNamed_AndSetOnesAreNot()
    {
        using var env = new EnvScope(
            ("MCPT_PART_A", null), ("MCPT_PART_B", null), ("MCPT_PART_SET", "topsecretvalue"));
        using var test = new TestConfig(
            Config("User Id=${MCPT_PART_A};Password=${MCPT_PART_B};Key=${MCPT_PART_SET};"));

        var ex = Assert.Throws<InvalidOperationException>(() => test.ConfigService.GetConnectionString());
        Assert.Contains("MCPT_PART_A", ex.Message);
        Assert.Contains("MCPT_PART_B", ex.Message);
        Assert.DoesNotContain("MCPT_PART_SET", ex.Message);
        Assert.DoesNotContain("topsecretvalue", ex.Message);
    }

    [Fact]
    public void OtherConfigurations_LoadNormally_WhenOneHasMissingVariable()
    {
        using var env = new EnvScope(("MCPT_OTHER_MISSING", null));
        using var test = new TestConfig(
            Config("Server=ok;"),
            new Dictionary<string, string> { ["broken"] = Config("Password=${MCPT_OTHER_MISSING};") });

        Assert.Contains("broken", test.ConfigService.ListConfigurations());
        Assert.Equal("Server=ok;", test.ConfigService.GetConnectionString());

        test.ConfigService.SwitchConfiguration("broken");
        Assert.Throws<InvalidOperationException>(() => test.ConfigService.GetConnectionString());
    }

    [Fact]
    public void ConfigurationInfo_DoesNotExposeConnectionStringOrSecret()
    {
        using var env = new EnvScope(("MCPT_INFO_PWD", "Un1queS3cretValue"));
        using var test = new TestConfig(Config("Server=db;Password=${MCPT_INFO_PWD};"));

        var info = test.ConfigService.GetCurrentConfigurationInfo();
        Assert.DoesNotContain("Un1queS3cretValue", info);
        Assert.DoesNotContain("MCPT_INFO_PWD", info);
        Assert.DoesNotContain("Server=db", info);
    }

    [Fact]
    public async Task TestConnection_OnUnresolvedConfiguration_ReturnsErrorNamingVariable()
    {
        using var env = new EnvScope(("MCPT_TC_MISSING", null));
        using var test = new TestConfig(Config("Server=127.0.0.1,1;Password=${MCPT_TC_MISSING};"));
        var db = new DatabaseService(test.ConfigService, NullLogger<DatabaseService>.Instance);

        var result = await db.TestConnectionDetailedAsync();

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("Success").GetBoolean());
        Assert.Contains("MCPT_TC_MISSING", doc.RootElement.GetProperty("Error").GetString());
    }

    [Fact]
    public async Task FailedConnection_ErrorDoesNotContainSecret()
    {
        using var env = new EnvScope(("MCPT_FAIL_PWD", "Un1queS3cretValue"));
        using var test = new TestConfig(Config(
            "Server=127.0.0.1,1;User Id=u;Password=${MCPT_FAIL_PWD};Connect Timeout=2;TrustServerCertificate=True;"));
        var db = new DatabaseService(test.ConfigService, NullLogger<DatabaseService>.Instance);

        var result = await db.TestConnectionDetailedAsync();

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("Success").GetBoolean());
        Assert.DoesNotContain("Un1queS3cretValue", result);
    }
}
