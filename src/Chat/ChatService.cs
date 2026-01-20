using Azure.AI.OpenAI;
using ModelContextProtocol.Client;
using OpenAI.Chat;

namespace McpEnterpriseClient.Chat;

/// <summary>
/// Handles the interactive chat conversation loop.
/// </summary>
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

    private void HandleFinalResponse(OpenAI.Chat.ChatCompletion responseMessage)
    {
        var textContent = responseMessage.Content[0].Text;
        Console.WriteLine(textContent);
        Console.WriteLine();

        _messages.Add(new AssistantChatMessage(responseMessage));
    }

    private static bool IsExitCommand(string input) =>
        input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase);

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
