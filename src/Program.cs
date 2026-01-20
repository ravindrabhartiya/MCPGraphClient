// ============================================================================
// MCP Enterprise Client - Main Entry Point
// ============================================================================
// This application connects to Microsoft's MCP (Model Context Protocol) Server
// for Enterprise, enabling AI-powered natural language queries against your
// Microsoft Entra tenant data using Microsoft Graph API.
//
// Architecture:
//   User Question → Azure OpenAI (GPT-4o) → MCP Tools → Graph API → Tenant Data
//
// Key Components:
//   - ConfigurationLoader: Loads settings from appsettings.json, env vars, Key Vault
//   - AuthenticationService: Handles Azure AD authentication (secret, cert, or interactive)
//   - McpClientService: Establishes SSE connection to MCP Server
//   - ChatService: Manages the interactive conversation loop with tool calling
// ============================================================================

using Azure.AI.OpenAI;
using McpEnterpriseClient.Authentication;
using McpEnterpriseClient.Chat;
using McpEnterpriseClient.Configuration;
using McpEnterpriseClient.Mcp;
using McpEnterpriseClient.Utilities;

namespace McpEnterpriseClient;

/// <summary>
/// Application entry point. Orchestrates the initialization of all services
/// and starts the interactive chat session.
/// </summary>
class Program
{
    /// <summary>
    /// Main entry point. Initializes configuration, authentication, MCP connection,
    /// and starts the interactive chat loop.
    /// </summary>
    /// <param name="args">Command line arguments (not currently used).</param>
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Microsoft MCP Server for Enterprise - C# Client ===\n");

        try
        {
            // Load configuration
            var configLoader = new ConfigurationLoader();
            var configuration = configLoader.LoadConfiguration();
            var settings = new AppSettings(configuration);

            PrintStartupInfo(settings);

            // Create Azure OpenAI client
            var openAIClient = new AzureOpenAIClient(
                new Uri(settings.AzureOpenAIEndpoint),
                new System.ClientModel.ApiKeyCredential(settings.AzureOpenAIKey));

            Console.WriteLine("✓ Azure OpenAI client initialized");

            // Authenticate user
            Console.WriteLine("\nAttempting to connect to MCP Server...");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("Note: MCP Server requires delegated (user) permissions.\n");

            var authService = new AuthenticationService();
            var authResult = await authService.AuthenticateAsync(
                settings.TenantId,
                settings.ClientId,
                settings.ClientSecret);

            // Connect to MCP server
            var mcpService = new McpClientService();
            var mcpClient = await mcpService.CreateClientAsync(
                settings.McpServerUrl,
                authResult.AccessToken);

            // List available tools
            var tools = await mcpService.ListToolsAsync(mcpClient);

            // Start interactive conversation
            var chatService = new ChatService(
                openAIClient,
                settings.ModelDeploymentName,
                tools);

            await chatService.RunAsync();
        }
        catch (Exception ex)
        {
            ErrorHandler.HandleException(ex);
        }
    }

    /// <summary>
    /// Prints startup configuration information to the console.
    /// Helps users verify their configuration is loaded correctly.
    /// </summary>
    /// <param name="settings">The loaded application settings.</param>
    private static void PrintStartupInfo(AppSettings settings)
    {
        Console.WriteLine($"Connecting to Azure OpenAI: {settings.AzureOpenAIEndpoint}");
        Console.WriteLine($"Using model deployment: {settings.ModelDeploymentName}");
        Console.WriteLine($"MCP Server: {settings.McpServerUrl}");
        Console.WriteLine($"Using app registration: {settings.ClientId}\n");
    }
}
