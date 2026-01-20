// ============================================================================
// Configuration Integration Tests
// ============================================================================
// End-to-end tests for configuration loading behavior.
// Tests environment variable fallback and config precedence.
//
// Test Collection:
//   Uses [Collection("Environment Variables")] to prevent parallel execution
//   with other tests that modify environment variables.
// ============================================================================

using McpEnterpriseClient.Configuration;
using Microsoft.Extensions.Configuration;

namespace McpEnterpriseClient.Tests.Integration;

/// <summary>
/// Integration tests for configuration loading and precedence.
/// </summary>
/// <remarks>
/// <para>
/// Tests verify:
/// </para>
/// <list type="bullet">
/// <item>Configuration can be loaded from environment variables</item>
/// <item>appsettings.json values override environment variables</item>
/// <item>ConfigurationLoader initializes correctly</item>
/// </list>
/// </remarks>
[Collection("Environment Variables")]
public class ConfigurationIntegrationTests
{
    [Fact]
    public void ConfigurationLoader_CanBeInstantiated()
    {
        // Act
        var loader = new ConfigurationLoader();

        // Assert
        Assert.NotNull(loader);
    }

    [Fact]
    public void AppSettings_FromEnvironmentVariables_WorksCorrectly()
    {
        // Arrange - Use environment variables as fallback
        // Note: AppSettings reads env vars directly, not through IConfiguration
        var originalTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        var originalClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var originalEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var originalApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

        try
        {
            // Set env vars BEFORE building config (AppSettings reads them directly)
            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", "env-tenant-id");
            Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", "env-client-id");
            Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", "https://env.openai.azure.com");
            Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", "env-api-key");

            // Empty config - AppSettings will fall back to environment variables
            var config = new ConfigurationBuilder().Build();

            // Act
            var settings = new AppSettings(config);

            // Assert - values come from environment variables as fallback
            Assert.Equal("env-tenant-id", settings.TenantId);
            Assert.Equal("env-client-id", settings.ClientId);
            Assert.Equal("https://env.openai.azure.com", settings.AzureOpenAIEndpoint);
            Assert.Equal("env-api-key", settings.AzureOpenAIKey);
        }
        finally
        {
            // Restore original environment variables
            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", originalTenantId);
            Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", originalClientId);
            Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void AppSettings_ConfigOverridesEnvironment_WorksCorrectly()
    {
        // Arrange
        var originalTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        
        try
        {
            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", "env-tenant-id");

            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureAD:TenantId"] = "config-tenant-id",
                    ["AzureAD:ClientId"] = "config-client-id",
                    ["AzureOpenAI:Endpoint"] = "https://config.openai.azure.com",
                    ["AzureOpenAI:ApiKey"] = "config-api-key"
                })
                .Build();

            // Act
            var settings = new AppSettings(config);

            // Assert - Config values should take precedence
            Assert.Equal("config-tenant-id", settings.TenantId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", originalTenantId);
        }
    }
}
