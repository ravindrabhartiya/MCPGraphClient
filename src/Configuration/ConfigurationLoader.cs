// ============================================================================
// Configuration Loader
// ============================================================================
// Loads application configuration from multiple sources in priority order:
//   1. appsettings.json - Base configuration file
//   2. Environment variables - Override for CI/CD and containers
//   3. Azure Key Vault (optional) - Secure secret management for production
// ============================================================================

using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace McpEnterpriseClient.Configuration;

/// <summary>
/// Loads and merges configuration from multiple sources.
/// Supports appsettings.json, environment variables, and Azure Key Vault.
/// </summary>
/// <remarks>
/// Configuration priority (highest to lowest):
/// <list type="number">
/// <item>Azure Key Vault (if configured)</item>
/// <item>Environment variables</item>
/// <item>appsettings.json</item>
/// </list>
/// </remarks>
public class ConfigurationLoader
{
    public IConfigurationRoot LoadConfiguration()
    {
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        var baseConfig = configBuilder.Build();

        var keyVaultUri = baseConfig["KeyVault:Uri"] ??
            Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI");

        if (!string.IsNullOrEmpty(keyVaultUri))
        {
            return LoadWithKeyVault(configBuilder, baseConfig, keyVaultUri);
        }

        return baseConfig;
    }

    private IConfigurationRoot LoadWithKeyVault(
        IConfigurationBuilder configBuilder,
        IConfigurationRoot baseConfig,
        string keyVaultUri)
    {
        Console.WriteLine($"Loading secrets from Key Vault: {keyVaultUri}");
        try
        {
            var kvCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = true
            });

            configBuilder.AddAzureKeyVault(new Uri(keyVaultUri), kvCredential);
            var configuration = configBuilder.Build();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Secrets loaded from Key Vault\n");
            Console.ResetColor();

            return configuration;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ Could not access Key Vault: {ex.Message}");
            Console.WriteLine("  Falling back to local configuration...\n");
            Console.ResetColor();
            return baseConfig;
        }
    }
}
