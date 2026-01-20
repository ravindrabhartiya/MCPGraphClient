// ============================================================================
// MCP Client Service
// ============================================================================
// Establishes connection to Microsoft's MCP (Model Context Protocol) Server
// using SSE (Server-Sent Events) transport.
//
// MCP Server provides three tools for Microsoft Graph queries:
//   1. microsoft_graph_suggest_queries - Discovers appropriate API endpoints
//   2. microsoft_graph_get - Executes Graph API calls
//   3. microsoft_graph_list_properties - Explores entity schemas
//
// Connection Flow:
//   1. Create SSE transport with Bearer token authentication
//   2. Connect to MCP server endpoint
//   3. Discover and list available tools
// ============================================================================

using ModelContextProtocol.Client;

namespace McpEnterpriseClient.Mcp;

/// <summary>
/// Manages MCP client connection and tool discovery.
/// </summary>
/// <remarks>
/// <para>
/// Uses SSE (Server-Sent Events) transport to maintain a persistent connection
/// to Microsoft's enterprise MCP server.
/// </para>
/// <para>
/// The server endpoint is: <c>https://mcp.svc.cloud.microsoft/enterprise</c>
/// </para>
/// </remarks>
public class McpClientService
{
    /// <summary>
    /// Creates and connects an MCP client to the specified server.
    /// </summary>
    /// <param name="serverUrl">The MCP server endpoint URL.</param>
    /// <param name="accessToken">The Bearer token for authentication.</param>
    /// <returns>A connected <see cref="IMcpClient"/> instance.</returns>
    /// <remarks>
    /// Uses SSE (Server-Sent Events) transport with the access token
    /// passed in the Authorization header.
    /// </remarks>
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

    /// <summary>
    /// Lists all available tools from the connected MCP server.
    /// </summary>
    /// <param name="client">The connected MCP client.</param>
    /// <returns>A list of available <see cref="McpClientTool"/> instances.</returns>
    /// <remarks>
    /// Prints each tool's name and description to the console.
    /// </remarks>
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
