// ============================================================================
// Application Settings
// ============================================================================
// Strongly-typed configuration with validation. Reads from IConfiguration
// with environment variable fallback for each setting.
//
// Required Settings:
//   - TenantId: Azure AD tenant ID
//   - ClientId: App registration client ID
//   - AzureOpenAIEndpoint: Azure OpenAI resource endpoint
//   - AzureOpenAIKey: API key for Azure OpenAI
//
// Optional Settings (have defaults):
//   - ClientSecret: For confidential client auth (null = public client)
//   - ModelDeploymentName: GPT model name (default: "gpt-4o")
//   - McpServerUrl: MCP endpoint (default: Microsoft's enterprise server)
// ============================================================================

using Microsoft.Extensions.Configuration;

namespace McpEnterpriseClient.Configuration;

/// <summary>
/// Strongly-typed application settings with validation.
/// Loads values from IConfiguration with environment variable fallback.
/// </summary>
/// <remarks>
/// Each setting is read using a two-tier lookup:
/// <list type="number">
/// <item>Check IConfiguration (appsettings.json, Key Vault, etc.)</item>
/// <item>Fall back to environment variable if config value is empty</item>
/// </list>
/// Required settings throw <see cref="InvalidOperationException"/> if not configured.
/// </remarks>
public class AppSettings
{
    public string TenantId { get; }
    public string ClientId { get; }
    public string? ClientSecret { get; }
    public string AzureOpenAIEndpoint { get; }
    public string AzureOpenAIKey { get; }
    public string ModelDeploymentName { get; }
    public string McpServerUrl { get; }

    /// <summary>
    /// Initializes application settings from the provided configuration.
    /// </summary>
    /// <param name="configuration">The configuration source (appsettings.json, env vars, etc.).</param>
    /// <exception cref="InvalidOperationException">Thrown when required settings are missing.</exception>
    public AppSettings(IConfiguration configuration)
    {
        TenantId = GetRequiredValue(configuration, "AzureAD:TenantId", "AZURE_TENANT_ID", "Azure Tenant ID");
        ClientId = GetRequiredValue(configuration, "AzureAD:ClientId", "AZURE_CLIENT_ID", "Azure Client ID");
        
        ClientSecret = GetOptionalValue(configuration, "AzureAD:ClientSecret", "AZURE_CLIENT_SECRET");

        AzureOpenAIEndpoint = GetRequiredValue(configuration, "AzureOpenAI:Endpoint", "AZURE_OPENAI_ENDPOINT", "Azure OpenAI endpoint");
        AzureOpenAIKey = GetRequiredValue(configuration, "AzureOpenAI:ApiKey", "AZURE_OPENAI_API_KEY", "Azure OpenAI API Key");

        ModelDeploymentName = GetOptionalValue(configuration, "AzureOpenAI:DeploymentName", "AZURE_OPENAI_DEPLOYMENT_NAME") 
            ?? "gpt-4o";

        McpServerUrl = GetOptionalValue(configuration, "McpServer:Endpoint", null) 
            ?? "https://mcp.svc.cloud.microsoft/enterprise";
    }

    /// <summary>
    /// Gets a required configuration value from config or environment variable.
    /// </summary>
    /// <param name="config">The configuration source.</param>
    /// <param name="configKey">The key path in configuration (e.g., "AzureAD:TenantId").</param>
    /// <param name="envVar">The environment variable name to use as fallback.</param>
    /// <param name="displayName">Human-readable name for error messages.</param>
    /// <returns>The configuration value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when value is not configured.</exception>
    private static string GetRequiredValue(IConfiguration config, string configKey, string envVar, string displayName)
    {
        var value = config[configKey];
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        value = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        throw new InvalidOperationException($"{displayName} not configured");
    }

    /// <summary>
    /// Gets an optional configuration value from config or environment variable.
    /// </summary>
    /// <param name="config">The configuration source.</param>
    /// <param name="configKey">The key path in configuration.</param>
    /// <param name="envVar">The environment variable name (null to skip env var check).</param>
    /// <returns>The configuration value, or null if not configured.</returns>
    private static string? GetOptionalValue(IConfiguration config, string configKey, string? envVar)
    {
        var value = config[configKey];
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        if (envVar != null)
        {
            value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
