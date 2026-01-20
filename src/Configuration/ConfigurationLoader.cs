using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace McpEnterpriseClient.Configuration;

/// <summary>
/// Handles loading configuration from various sources including Key Vault.
/// </summary>
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
