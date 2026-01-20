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
    /// <summary>
    /// Loads configuration from appsettings.json, environment variables, and optionally Azure Key Vault.
    /// </summary>
    /// <returns>The merged <see cref="IConfigurationRoot"/> containing all settings.</returns>
    /// <remarks>
    /// If <c>KeyVault:Uri</c> or <c>AZURE_KEYVAULT_URI</c> is configured,
    /// secrets are loaded from Key Vault using <see cref="Azure.Identity.DefaultAzureCredential"/>.
    /// </remarks>
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

    /// <summary>
    /// Loads configuration with Azure Key Vault integration.
    /// </summary>
    /// <param name="configBuilder">The configuration builder to add Key Vault to.</param>
    /// <param name="baseConfig">The base configuration (fallback if Key Vault fails).</param>
    /// <param name="keyVaultUri">The URI of the Azure Key Vault.</param>
    /// <returns>Configuration with Key Vault secrets merged in.</returns>
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
