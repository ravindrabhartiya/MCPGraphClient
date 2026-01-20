// ============================================================================
// Chat Service
// ============================================================================
// Manages the interactive conversation loop between the user and Azure OpenAI.
// Handles the complete tool calling workflow:
//
//   1. User asks a question
//   2. Azure OpenAI determines which MCP tools to call
//   3. ToolExecutor invokes the MCP tools
//   4. Results are fed back to Azure OpenAI
//   5. Azure OpenAI generates a natural language response
//
// The conversation maintains message history for context-aware responses.
// Tool calling can iterate up to MaxIterations (10) times per question.
// ============================================================================

using Azure.AI.OpenAI;
using ModelContextProtocol.Client;
using OpenAI.Chat;

namespace McpEnterpriseClient.Chat;

/// <summary>
/// Manages the interactive chat loop with Azure OpenAI and MCP tool calling.
/// </summary>
/// <remarks>
/// <para>
/// The service maintains conversation history across multiple exchanges,
/// allowing for context-aware follow-up questions.
/// </para>
/// <para>
/// Tool calling workflow:
/// </para>
/// <list type="number">
/// <item>Send user message to Azure OpenAI with available tools</item>
/// <item>If OpenAI returns tool calls, execute them via ToolExecutor</item>
/// <item>Feed tool results back to OpenAI</item>
/// <item>Repeat until OpenAI provides a final text response</item>
/// </list>
/// </remarks>
public class ChatService
{
    private const int MaxIterations = 10;
    private const string SystemPrompt = 
        "You are a helpful assistant that can query Microsoft Entra tenant data using Microsoft Graph API. " +
        "You have access to tools that let you discover and execute Graph API calls. " +
        "Always use microsoft_graph_suggest_queries first to find the right API endpoint, " +
        "then use microsoft_graph_get to execute it.";

    private readonly ChatClient _chatClient;
    private readonly List<ChatTool> _chatTools;
    private readonly ToolExecutor _toolExecutor;
    private readonly List<ChatMessage> _messages = new();

    /// <summary>
    /// Initializes a new ChatService with OpenAI client and MCP tools.
    /// </summary>
    /// <param name="openAIClient">The Azure OpenAI client.</param>
    /// <param name="deploymentName">The model deployment name (e.g., "gpt-4o").</param>
    /// <param name="mcpTools">The list of MCP tools available for the AI to call.</param>
    public ChatService(
        AzureOpenAIClient openAIClient,
        string deploymentName,
        IList<McpClientTool> mcpTools)
    {
        _chatClient = openAIClient.GetChatClient(deploymentName);
        
        var toolConverter = new ToolConverter();
        _chatTools = toolConverter.ConvertMcpToolsToChatTools(mcpTools);
        _toolExecutor = new ToolExecutor(mcpTools);

        _messages.Add(new SystemChatMessage(SystemPrompt));
    }

    /// <summary>
    /// Starts the interactive chat loop. Runs until user types 'exit' or 'quit'.
    /// </summary>
    /// <returns>A task that completes when the user exits.</returns>
    public async Task RunAsync()
    {
        PrintWelcome();

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("You: ");
            Console.ResetColor();

            var userInput = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(userInput))
                continue;

            if (IsExitCommand(userInput))
            {
                Console.WriteLine("\nGoodbye!");
                break;
            }

            await ProcessUserInputAsync(userInput);
        }
    }

    /// <summary>
    /// Processes a single user input message through the AI pipeline.
    /// </summary>
    /// <param name="userInput">The user's message text.</param>
    private async Task ProcessUserInputAsync(string userInput)
    {
        _messages.Add(new UserChatMessage(userInput));

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("\nAssistant: ");
        Console.ResetColor();

        try
        {
            await ProcessConversationLoopAsync();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n✗ Error during conversation: {ex.Message}\n");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Executes the conversation loop with tool calling until a final response is received.
    /// </summary>
    /// <remarks>
    /// Iterates up to <see cref="MaxIterations"/> times, calling tools as requested by the AI.
    /// </remarks>
    private async Task ProcessConversationLoopAsync()
    {
        int iteration = 0;

        while (iteration < MaxIterations)
        {
            iteration++;

            var chatOptions = new ChatCompletionOptions();
            foreach (var tool in _chatTools)
            {
                chatOptions.Tools.Add(tool);
            }

            var completion = await _chatClient.CompleteChatAsync(_messages, chatOptions);
            var responseMessage = completion.Value;

            if (responseMessage.ToolCalls.Count > 0)
            {
                await HandleToolCallsAsync(responseMessage);
            }
            else
            {
                HandleFinalResponse(responseMessage);
                return;
            }
        }

        PrintMaxIterationsWarning();
    }

    /// <summary>
    /// Handles tool calls from the AI response by executing each tool.
    /// </summary>
    /// <param name="responseMessage">The AI response containing tool calls.</param>
    private async Task HandleToolCallsAsync(OpenAI.Chat.ChatCompletion responseMessage)
    {
        _messages.Add(new AssistantChatMessage(responseMessage));

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[Calling {responseMessage.ToolCalls.Count} tool(s)...]");
        Console.ResetColor();

        foreach (var toolCall in responseMessage.ToolCalls)
        {
            if (toolCall.Kind == ChatToolCallKind.Function)
            {
                var result = await _toolExecutor.ExecuteToolCallAsync(toolCall);
                _messages.Add(result);
            }
        }
    }

    /// <summary>
    /// Handles the final text response from the AI (no more tool calls).
    /// </summary>
    /// <param name="responseMessage">The AI response containing the final text.</param>
    private void HandleFinalResponse(OpenAI.Chat.ChatCompletion responseMessage)
    {
        var textContent = responseMessage.Content[0].Text;
        Console.WriteLine(textContent);
        Console.WriteLine();

        _messages.Add(new AssistantChatMessage(responseMessage));
    }

    /// <summary>
    /// Checks if the user input is an exit command.
    /// </summary>
    /// <param name="input">The user input to check.</param>
    /// <returns>True if the input is 'exit' or 'quit' (case-insensitive).</returns>
    private static bool IsExitCommand(string input) =>
        input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Prints the welcome message with example queries.
    /// </summary>
    private static void PrintWelcome()
    {
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("Interactive Mode - Ask questions about your Microsoft Entra tenant");
        Console.WriteLine("Examples:");
        Console.WriteLine("  • How many users do we have in our tenant?");
        Console.WriteLine("  • List all users who didn't sign in last month");
        Console.WriteLine("  • Show me all guest users");
        Console.WriteLine("  • Is MFA enabled for all administrators?");
        Console.WriteLine("Type 'exit' or 'quit' to end the session");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
    }

    private static void PrintMaxIterationsWarning()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[Maximum iterations reached. The query may be too complex.]");
        Console.ResetColor();
    }
}
