// ============================================================================
// Tool Executor
// ============================================================================
// Executes MCP tool calls requested by Azure OpenAI and handles responses.
//
// Responsibilities:
//   - Parse tool call arguments from OpenAI
//   - Invoke the corresponding MCP tool
//   - Format results as ToolChatMessage for OpenAI
//   - Detect and report server errors (401, 403, 404, etc.)
//   - Provide debugging information for troubleshooting
//
// Error Handling:
//   - Server errors are detected by parsing response JSON
//   - Helpful hints are printed for common issues (auth, permissions)
//   - Errors are wrapped in ToolChatMessage for OpenAI to interpret
// ============================================================================

using ModelContextProtocol.Client;
using OpenAI.Chat;
using System.Text.Json;

namespace McpEnterpriseClient.Chat;

/// <summary>
/// Executes MCP tool calls and formats responses for Azure OpenAI.
/// </summary>
/// <remarks>
/// <para>
/// When Azure OpenAI determines a tool should be called, this class:
/// </para>
/// <list type="number">
/// <item>Parses the JSON arguments from the tool call</item>
/// <item>Finds and invokes the corresponding MCP tool</item>
/// <item>Checks the response for server errors</item>
/// <item>Returns results as a <see cref="ToolChatMessage"/></item>
/// </list>
/// <para>
/// Debug output is printed to console for troubleshooting.
/// </para>
/// </remarks>
public class ToolExecutor
{
    private readonly IList<McpClientTool> _mcpTools;

    /// <summary>
    /// Initializes a new ToolExecutor with the available MCP tools.
    /// </summary>
    /// <param name="mcpTools">The list of MCP tools that can be executed.</param>
    public ToolExecutor(IList<McpClientTool> mcpTools)
    {
        _mcpTools = mcpTools;
    }

    /// <summary>
    /// Executes an MCP tool call and returns the result as a chat message.
    /// </summary>
    /// <param name="toolCall">The tool call from Azure OpenAI.</param>
    /// <returns>A <see cref="ToolChatMessage"/> containing the tool result.</returns>
    public async Task<ToolChatMessage> ExecuteToolCallAsync(ChatToolCall toolCall)
    {
        var toolName = toolCall.FunctionName;
        var toolArgsString = toolCall.FunctionArguments.ToString();

        Console.WriteLine($"  → {toolName}({toolArgsString})");

        var mcpTool = _mcpTools.FirstOrDefault(t => t.Name == toolName);
        if (mcpTool == null)
        {
            var errorResult = new { error = $"Tool '{toolName}' not found" };
            return new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(errorResult));
        }

        try
        {
            var argsDict = string.IsNullOrEmpty(toolArgsString) || toolArgsString == "{}"
                ? new Dictionary<string, object?>()
                : JsonSerializer.Deserialize<Dictionary<string, object?>>(toolArgsString);

            PrintDebugInfo(toolName, toolArgsString);

            var result = await mcpTool.CallAsync(argsDict);
            var resultJson = JsonSerializer.Serialize(result);

            CheckForServerErrors(resultJson, toolName, toolArgsString);
            PrintResult(resultJson);

            return new ToolChatMessage(toolCall.Id, resultJson);
        }
        catch (Exception ex)
        {
            return HandleToolException(toolCall.Id, toolName, toolArgsString, ex);
        }
    }

    /// <summary>
    /// Prints debug information about the tool being called.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="toolArgsString">The JSON arguments string.</param>
    private static void PrintDebugInfo(string toolName, string toolArgsString)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    [DEBUG] Calling tool: {toolName}");
        Console.WriteLine($"    [DEBUG] Arguments: {toolArgsString}");
        Console.ResetColor();
    }

    /// <summary>
    /// Prints the tool result (truncated if too long).
    /// </summary>
    /// <param name="resultJson">The JSON result from the tool.</param>
    private static void PrintResult(string resultJson)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    Result: {(resultJson.Length > 2048 ? resultJson[..2048] + "..." : resultJson)}");
        Console.ResetColor();
    }

    /// <summary>
    /// Checks the tool response for server-side errors and prints diagnostics.
    /// </summary>
    /// <param name="resultJson">The JSON result to check.</param>
    /// <param name="toolName">The name of the tool (for error reporting).</param>
    /// <param name="toolArgsString">The arguments (for error reporting).</param>
    private static void CheckForServerErrors(string resultJson, string toolName, string toolArgsString)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(resultJson);
            var root = jsonDoc.RootElement;

            bool hasServerError = false;
            string? serverErrorMessage = null;

            if (root.TryGetProperty("isError", out var isErrorProp) &&
                isErrorProp.ValueKind == JsonValueKind.True)
            {
                hasServerError = true;
            }

            if (root.TryGetProperty("content", out var contentArray) &&
                contentArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in contentArray.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var textProp))
                    {
                        var text = textProp.GetString() ?? "";
                        if (IsErrorText(text))
                        {
                            hasServerError = true;
                            serverErrorMessage = text;
                        }
                    }
                }
            }

            if (hasServerError)
            {
                PrintServerError(toolName, toolArgsString, serverErrorMessage);
            }
        }
        catch { /* Ignore JSON parsing errors */ }
    }

    /// <summary>
    /// Checks if text contains error indicators.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>True if the text appears to be an error message.</returns>
    private static bool IsErrorText(string text) =>
        text.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("error:", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("No scopes found", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("forbidden", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Prints a formatted server error message with diagnostic hints.
    /// </summary>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="toolArgsString">The arguments passed to the tool.</param>
    /// <param name="serverErrorMessage">The error message from the server.</param>
    private static void PrintServerError(string toolName, string toolArgsString, string? serverErrorMessage)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n    ╔══════════════════════════════════════════════════════════════");
        Console.WriteLine($"    ║ MCP SERVER ERROR RESPONSE");
        Console.WriteLine($"    ╠══════════════════════════════════════════════════════════════");
        Console.WriteLine($"    ║ Tool Name:     {toolName}");
        Console.WriteLine($"    ║ Arguments:     {toolArgsString}");
        Console.WriteLine($"    ║ Server Error:  {serverErrorMessage ?? "See response below"}");

        if (serverErrorMessage?.Contains("No scopes found", StringComparison.OrdinalIgnoreCase) == true)
        {
            PrintScopeMismatchHint();
        }
        else if (serverErrorMessage?.Contains("401", StringComparison.OrdinalIgnoreCase) == true ||
                 serverErrorMessage?.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) == true)
        {
            Console.WriteLine($"    ║ ────────────────────────────────────────────────────────────");
            Console.WriteLine($"    ║ HINT: 401 Unauthorized - Authentication failed");
            Console.WriteLine($"    ║       The token may be invalid or expired");
        }
        else if (serverErrorMessage?.Contains("403", StringComparison.OrdinalIgnoreCase) == true ||
                 serverErrorMessage?.Contains("forbidden", StringComparison.OrdinalIgnoreCase) == true)
        {
            Console.WriteLine($"    ║ ────────────────────────────────────────────────────────────");
            Console.WriteLine($"    ║ HINT: 403 Forbidden - Insufficient permissions");
            Console.WriteLine($"    ║       Add required Graph API permissions in Azure AD");
        }

        Console.WriteLine($"    ╚══════════════════════════════════════════════════════════════\n");
        Console.ResetColor();
    }

    /// <summary>
    /// Prints hints for token scope mismatch errors.
    /// </summary>
    private static void PrintScopeMismatchHint()
    {
        Console.WriteLine($"    ║ ────────────────────────────────────────────────────────────");
        Console.WriteLine($"    ║ DIAGNOSIS: Token scope mismatch");
        Console.WriteLine($"    ║ ");
        Console.WriteLine($"    ║ The MCP server expects a DELEGATED (user) token with scopes");
        Console.WriteLine($"    ║ like 'User.Read', but you're using a CLIENT CREDENTIALS token");
        Console.WriteLine($"    ║ which has APPLICATION permissions (no user scopes).");
        Console.WriteLine($"    ║ ");
        Console.WriteLine($"    ║ POSSIBLE SOLUTIONS:");
        Console.WriteLine($"    ║ 1. Use interactive auth (DeviceCodeCredential) for user token");
        Console.WriteLine($"    ║ 2. Use OnBehalfOfCredential if you have a user assertion");
        Console.WriteLine($"    ║ 3. Check if MCP server supports app-only authentication");
    }

    /// <summary>
    /// Handles exceptions from tool execution and returns an error message.
    /// </summary>
    /// <param name="toolCallId">The ID of the tool call.</param>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="toolArgsString">The arguments passed to the tool.</param>
    /// <param name="ex">The exception that occurred.</param>
    /// <returns>A <see cref="ToolChatMessage"/> containing the error details.</returns>
    private static ToolChatMessage HandleToolException(
        string toolCallId,
        string toolName,
        string toolArgsString,
        Exception ex)
    {
        var errorResult = new
        {
            error = ex.Message,
            errorType = ex.GetType().FullName,
            tool = toolName,
            arguments = toolArgsString
        };

        PrintToolError(toolName, toolArgsString, ex);

        return new ToolChatMessage(toolCallId, JsonSerializer.Serialize(errorResult));
    }

    /// <summary>
    /// Prints detailed error information for a failed tool call.
    /// </summary>
    /// <param name="toolName">The name of the tool that failed.</param>
    /// <param name="toolArgsString">The arguments passed to the tool.</param>
    /// <param name="ex">The exception that occurred.</param>
    private static void PrintToolError(string toolName, string toolArgsString, Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n    ╔══════════════════════════════════════════════════════════════");
        Console.WriteLine($"    ║ TOOL ERROR DETAILS");
        Console.WriteLine($"    ╠══════════════════════════════════════════════════════════════");
        Console.WriteLine($"    ║ Tool Name:     {toolName}");
        Console.WriteLine($"    ║ Arguments:     {toolArgsString}");
        Console.WriteLine($"    ║ Error Type:    {ex.GetType().FullName}");
        Console.WriteLine($"    ║ Error Message: {ex.Message}");

        if (ex is HttpRequestException httpEx)
        {
            Console.WriteLine($"    ║ HTTP Status:   {httpEx.StatusCode}");
        }

        if (ex.InnerException != null)
        {
            Console.WriteLine($"    ║ ────────────────────────────────────────────────────────────");
            Console.WriteLine($"    ║ Inner Exception:");
            Console.WriteLine($"    ║   Type:    {ex.InnerException.GetType().FullName}");
            Console.WriteLine($"    ║   Message: {ex.InnerException.Message}");
        }

        PrintHttpHints(ex.Message);

        Console.WriteLine($"    ╠══════════════════════════════════════════════════════════════");
        Console.WriteLine($"    ║ Stack Trace (first 5 frames):");
        var stackLines = ex.StackTrace?.Split('\n').Take(5) ?? Array.Empty<string>();
        foreach (var line in stackLines)
        {
            Console.WriteLine($"    ║   {line.Trim()}");
        }
        Console.WriteLine($"    ╚══════════════════════════════════════════════════════════════\n");
        Console.ResetColor();
    }

    /// <summary>
    /// Prints helpful hints based on HTTP error codes in the message.
    /// </summary>
    /// <param name="errorMessage">The error message to analyze.</param>
    private static void PrintHttpHints(string errorMessage)
    {
        var errorMsg = errorMessage.ToLowerInvariant();

        if (errorMsg.Contains("401") || errorMsg.Contains("unauthorized"))
        {
            Console.WriteLine($"    ║ ────────────────────────────────────────────────────────────");
            Console.WriteLine($"    ║ HINT: 401 Unauthorized - Check API permissions");
            Console.WriteLine($"    ║       Ensure the app has the required Graph permissions");
        }
        else if (errorMsg.Contains("403") || errorMsg.Contains("forbidden"))
        {
            Console.WriteLine($"    ║ ────────────────────────────────────────────────────────────");
            Console.WriteLine($"    ║ HINT: 403 Forbidden - Insufficient permissions");
            Console.WriteLine($"    ║       The app may need admin consent for this operation");
        }
        else if (errorMsg.Contains("404") || errorMsg.Contains("not found"))
        {
            Console.WriteLine($"    ║ ────────────────────────────────────────────────────────────");
            Console.WriteLine($"    ║ HINT: 404 Not Found - Invalid endpoint or resource");
            Console.WriteLine($"    ║       Check the relativeUrl parameter");
        }
        else if (errorMsg.Contains("400") || errorMsg.Contains("bad request"))
        {
            Console.WriteLine($"    ║ ────────────────────────────────────────────────────────────");
            Console.WriteLine($"    ║ HINT: 400 Bad Request - Malformed request");
            Console.WriteLine($"    ║       Check the query parameters and syntax");
        }
    }
}
