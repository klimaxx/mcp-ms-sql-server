using ModelContextProtocol.Server;
using McpMsSqlServer.Services;
using System.ComponentModel;
using System.Text.Json;

namespace McpMsSqlServer.Tools;

[McpServerToolType]
public static class ConfigurationTools
{
    [McpServerTool]
    [Description("List all available project configurations. 'configurations' holds the identifiers to pass to SwitchConfiguration; 'details' pairs each identifier with its display name")]
    public static string ListConfigurations(ConfigService configService)
    {
        var configs = configService.ListConfigurations();
        var details = configService.ListConfigurationDetails()
            .Select(d => new { configuration = d.Configuration, name = d.Name })
            .ToList();
        return JsonSerializer.Serialize(new { configurations = configs, details }, new JsonSerializerOptions { WriteIndented = true });
    }
    
    [McpServerTool]
    [Description("Switch to a different project configuration")]
    public static string SwitchConfiguration(
        ConfigService configService,
        [Description("The configuration identifier (file name without .json, as returned by ListConfigurations); matched case-insensitively")] string configName)
    {
        return configService.SwitchConfiguration(configName);
    }
    
    [McpServerTool]
    [Description("Get current configuration details")]
    public static string GetCurrentConfiguration(ConfigService configService)
    {
        return configService.GetCurrentConfigurationInfo();
    }
}