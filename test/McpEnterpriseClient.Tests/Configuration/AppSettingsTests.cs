// ============================================================================
// AppSettings Unit Tests
// ============================================================================
// Tests for the AppSettings configuration class.
// Verifies configuration loading, validation, and environment variable fallback.
//
// Test Collection:
//   Uses [Collection("Environment Variables")] to prevent parallel execution
//   with other tests that modify environment variables.
// ============================================================================

using McpEnterpriseClient.Configuration;
using Microsoft.Extensions.Configuration;

namespace McpEnterpriseClient.Tests.Configuration;

/// <summary>
/// Test helper that temporarily clears environment variables for isolated testing.
/// Restores original values when disposed.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// using var scope = new EnvVarScope("VAR1", "VAR2");
/// // VAR1 and VAR2 are null during this block
/// // Original values restored when scope is disposed
/// </code>
/// </remarks>
public class EnvVarScope : IDisposable
{
    private readonly Dictionary<string, string?> _originalValues = new();
    private readonly string[] _varNames;

    public EnvVarScope(params string[] varNames)
    {
        _varNames = varNames;
        foreach (var name in varNames)
        {
            _originalValues[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    public void Dispose()
    {
        foreach (var kvp in _originalValues)
        {
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
        }
    }
}

/// <summary>
/// Unit tests for <see cref="AppSettings"/> configuration class.
/// </summary>
/// <remarks>
/// <para>
/// Tests verify:
/// </para>
/// <list type="bullet">
/// <item>All properties are correctly loaded from configuration</item>
/// <item>Optional values use appropriate defaults</item>
/// <item>Required values throw when missing</item>
/// <item>Empty/whitespace values are treated as missing</item>
/// </list>
/// </remarks>
[Collection("Environment Variables")]
public class AppSettingsTests
{
    /// <summary>
    /// Verifies that all properties are correctly populated from valid configuration.
    /// </summary>
    [Fact]
    public void Constructor_WithValidConfiguration_SetsAllProperties()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAD:TenantId"] = "test-tenant-id",
                ["AzureAD:ClientId"] = "test-client-id",
                ["AzureAD:ClientSecret"] = "test-secret",
                ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com",
                ["AzureOpenAI:ApiKey"] = "test-api-key",
                ["AzureOpenAI:DeploymentName"] = "gpt-4o-test",
                ["McpServer:Endpoint"] = "https://mcp.test.com"
            })
            .Build();

        // Act
        var settings = new AppSettings(config);

        // Assert
        Assert.Equal("test-tenant-id", settings.TenantId);
        Assert.Equal("test-client-id", settings.ClientId);
        Assert.Equal("test-secret", settings.ClientSecret);
        Assert.Equal("https://test.openai.azure.com", settings.AzureOpenAIEndpoint);
        Assert.Equal("test-api-key", settings.AzureOpenAIKey);
        Assert.Equal("gpt-4o-test", settings.ModelDeploymentName);
        Assert.Equal("https://mcp.test.com", settings.McpServerUrl);
    }

    [Fact]
    public void Constructor_WithMissingOptionalValues_UsesDefaults()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAD:TenantId"] = "test-tenant-id",
                ["AzureAD:ClientId"] = "test-client-id",
                ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com",
                ["AzureOpenAI:ApiKey"] = "test-api-key"
                // Missing: ClientSecret, DeploymentName, McpServer:Endpoint
            })
            .Build();

        // Act
        var settings = new AppSettings(config);

        // Assert
        Assert.Null(settings.ClientSecret);
        Assert.Equal("gpt-4o", settings.ModelDeploymentName); // Default
        Assert.Equal("https://mcp.svc.cloud.microsoft/enterprise", settings.McpServerUrl); // Default
    }

    [Fact]
    public void Constructor_WithMissingTenantId_InEmptyConfig_ThrowsException()
    {
        // Arrange - clear all Azure env vars to ensure clean test
        using var _ = new EnvVarScope("AZURE_TENANT_ID", "AZURE_CLIENT_ID", "AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_API_KEY");
        var config = new ConfigurationBuilder().Build();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => new AppSettings(config));
        Assert.Contains("Tenant ID", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptyStringValues_ThrowsException()
    {
        // Arrange - clear env vars and use empty strings in config
        using var _ = new EnvVarScope("AZURE_TENANT_ID", "AZURE_CLIENT_ID", "AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_API_KEY");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAD:TenantId"] = "",
                ["AzureAD:ClientId"] = "",
                ["AzureOpenAI:Endpoint"] = "",
                ["AzureOpenAI:ApiKey"] = ""
            })
            .Build();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => new AppSettings(config));
        Assert.Contains("not configured", exception.Message);
    }

    [Fact]
    public void Constructor_WithWhitespaceValues_ThrowsException()
    {
        // Arrange - clear env vars and use whitespace in config
        using var _ = new EnvVarScope("AZURE_TENANT_ID", "AZURE_CLIENT_ID", "AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_API_KEY");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAD:TenantId"] = "   ",
                ["AzureAD:ClientId"] = "test-client",
                ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com",
                ["AzureOpenAI:ApiKey"] = "test-key"
            })
            .Build();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => new AppSettings(config));
        Assert.Contains("Tenant ID", exception.Message);
    }
}
