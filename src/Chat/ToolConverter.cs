// ============================================================================
// Tool Converter
// ============================================================================
// Converts MCP tools to OpenAI ChatTool format for the function calling API.
// Each MCP tool needs a JSON schema defining its parameters.
//
// Supported MCP Tools:
//   - microsoft_graph_suggest_queries: Takes 'intentDescription' (string)
//   - microsoft_graph_get: Takes 'relativeUrl' (string)
//   - microsoft_graph_list_properties: Takes 'entityName' (string)
// ============================================================================

using ModelContextProtocol.Client;
using OpenAI.Chat;

namespace McpEnterpriseClient.Chat;

/// <summary>
/// Converts MCP tools to OpenAI ChatTool format for function calling.
/// </summary>
/// <remarks>
/// <para>
/// OpenAI's function calling API requires a JSON schema for each tool's
/// parameters. This class generates the appropriate schemas for known
/// Microsoft Graph MCP tools.
/// </para>
/// <para>
/// Unknown tools are created with empty parameter schemas.
/// </para>
/// </remarks>
public class ToolConverter
{
    public List<ChatTool> ConvertMcpToolsToChatTools(IList<McpClientTool> mcpTools)
    {
        var chatTools = new List<ChatTool>();

        foreach (var tool in mcpTools)
        {
            var parametersSchema = CreateParameterSchema(tool.Name);
            
            var functionDef = ChatTool.CreateFunctionTool(
                tool.Name,
                tool.Description ?? "No description available",
                parametersSchema);

            chatTools.Add(functionDef);
        }

        return chatTools;
    }

    private static BinaryData CreateParameterSchema(string toolName)
    {
        var schema = new
        {
            type = "object",
            properties = new Dictionary<string, object>(),
            required = new List<string>()
        };

        switch (toolName)
        {
            case "microsoft_graph_suggest_queries":
                schema.properties["intentDescription"] = new
                {
                    type = "string",
                    description = "The intent description or query to search for"
                };
                schema.required.Add("intentDescription");
                break;

            case "microsoft_graph_get":
                schema.properties["relativeUrl"] = new
                {
                    type = "string",
                    description = "The relative URL for the Microsoft Graph API call"
                };
                schema.required.Add("relativeUrl");
                break;

            case "microsoft_graph_list_properties":
                schema.properties["entityName"] = new
                {
                    type = "string",
                    description = "The entity name to list properties for"
                };
                schema.required.Add("entityName");
                break;
        }

        return BinaryData.FromObjectAsJson(schema);
    }
}
