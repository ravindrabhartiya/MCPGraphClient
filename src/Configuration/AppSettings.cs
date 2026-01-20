using Microsoft.Extensions.Configuration;

namespace McpEnterpriseClient.Configuration;

/// <summary>
/// Strongly-typed application settings.
/// </summary>
public class AppSettings
{
    public string TenantId { get; }
    public string ClientId { get; }
    public string? ClientSecret { get; }
    public string AzureOpenAIEndpoint { get; }
    public string AzureOpenAIKey { get; }
    public string ModelDeploymentName { get; }
    public string McpServerUrl { get; }

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
