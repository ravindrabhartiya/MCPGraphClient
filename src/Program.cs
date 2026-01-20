using Azure.AI.OpenAI;
using McpEnterpriseClient.Authentication;
using McpEnterpriseClient.Chat;
using McpEnterpriseClient.Configuration;
using McpEnterpriseClient.Mcp;
using McpEnterpriseClient.Utilities;

namespace McpEnterpriseClient;

class Program
{
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

    private static void PrintStartupInfo(AppSettings settings)
    {
        Console.WriteLine($"Connecting to Azure OpenAI: {settings.AzureOpenAIEndpoint}");
        Console.WriteLine($"Using model deployment: {settings.ModelDeploymentName}");
        Console.WriteLine($"MCP Server: {settings.McpServerUrl}");
        Console.WriteLine($"Using app registration: {settings.ClientId}\n");
    }
}
