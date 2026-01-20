using ModelContextProtocol.Client;

namespace McpEnterpriseClient.Mcp;

/// <summary>
/// Handles MCP client creation and connection.
/// </summary>
public class McpClientService
{
    public async Task<IMcpClient> CreateClientAsync(string serverUrl, string accessToken)
    {
        Console.WriteLine("\n[Step 2] Connecting to MCP server...");
        Console.WriteLine($"Configuring SSE transport to: {serverUrl}");

        var transportOptions = new SseClientTransportOptions
        {
            Endpoint = new Uri(serverUrl),
            AdditionalHeaders = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {accessToken}" }
            }
        };

        var transport = new SseClientTransport(transportOptions);

        Console.WriteLine("Creating MCP client...");
        var client = await McpClientFactory.CreateAsync(transport);
        
        Console.WriteLine("✓ MCP client connected to Microsoft Enterprise Server\n");
        return client;
    }

    public async Task<IList<McpClientTool>> ListToolsAsync(IMcpClient client)
    {
        Console.WriteLine("Available MCP Tools:");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        var tools = await client.ListToolsAsync();
        
        foreach (var tool in tools)
        {
            Console.WriteLine($"• {tool.Name}");
            Console.WriteLine($"  {tool.Description}");
            Console.WriteLine();
        }
        
        Console.WriteLine($"Total tools available: {tools.Count}\n");
        return tools;
    }
}
