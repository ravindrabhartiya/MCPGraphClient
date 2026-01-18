using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using ModelContextProtocol.Client;
using OpenAI.Chat;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace McpEnterpriseClient
{
    class Program
    {
        // Token cache file for persistent caching
        private static readonly string TokenCacheFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McpEnterpriseClient", "msal_token_cache.bin");
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Microsoft MCP Server for Enterprise - C# Client ===\n");

            // Load base configuration (without secrets)
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            
            var baseConfig = configBuilder.Build();
            
            // Check if Key Vault is configured
            var keyVaultUri = baseConfig["KeyVault:Uri"] ?? 
                Environment.GetEnvironmentVariable("AZURE_KEYVAULT_URI");
            
            IConfigurationRoot configuration;
            
            if (!string.IsNullOrEmpty(keyVaultUri))
            {
                Console.WriteLine($"Loading secrets from Key Vault: {keyVaultUri}");
                try
                {
                    // Use DefaultAzureCredential to access Key Vault
                    // Works with: Managed Identity (Azure), az login (local), VS credentials
                    var kvCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        ExcludeInteractiveBrowserCredential = true // Don't prompt for KV access
                    });
                    
                    configBuilder.AddAzureKeyVault(new Uri(keyVaultUri), kvCredential);
                    configuration = configBuilder.Build();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Secrets loaded from Key Vault\n");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠ Could not access Key Vault: {ex.Message}");
                    Console.WriteLine("  Falling back to local configuration...\n");
                    Console.ResetColor();
                    configuration = baseConfig;
                }
            }
            else
            {
                configuration = baseConfig;
            }

            // Get configuration values
            var tenantId = configuration["AzureAD:TenantId"] ?? 
                Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ??
                throw new InvalidOperationException("Azure Tenant ID not configured");
            
            var clientId = configuration["AzureAD:ClientId"] ?? 
                Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ??
                throw new InvalidOperationException("Azure Client ID not configured");
            
            // Secrets from Key Vault (naming: AzureAD--ClientSecret → AzureAD:ClientSecret)
            var clientSecret = configuration["AzureAD:ClientSecret"] ?? 
                Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");

            var azureOpenAIEndpoint = configuration["AzureOpenAI:Endpoint"] ?? 
                Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? 
                throw new InvalidOperationException("Azure OpenAI endpoint not configured");
            
            // API Key from Key Vault
            var azureOpenAIKey = configuration["AzureOpenAI:ApiKey"] ?? 
                Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? 
                throw new InvalidOperationException("Azure OpenAI API Key not configured");
            
            var modelDeploymentName = configuration["AzureOpenAI:DeploymentName"] ?? 
                Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? 
                "gpt-4o";

            var mcpServerUrl = configuration["McpServer:Endpoint"] ?? 
                "https://mcp.svc.cloud.microsoft/enterprise";

            try
            {
                Console.WriteLine($"Connecting to Azure OpenAI: {azureOpenAIEndpoint}");
                Console.WriteLine($"Using model deployment: {modelDeploymentName}");
                Console.WriteLine($"MCP Server: {mcpServerUrl}");
                Console.WriteLine($"Using app registration: {clientId}\n");
                
                // Create Azure OpenAI client with API key
                var openAIClient = new AzureOpenAIClient(
                    new Uri(azureOpenAIEndpoint),
                    new System.ClientModel.ApiKeyCredential(azureOpenAIKey));

                Console.WriteLine("✓ Azure OpenAI client initialized");

                // Create MCP client for Microsoft MCP Server for Enterprise
                Console.WriteLine("\nAttempting to connect to MCP Server...");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("Note: MCP Server requires delegated (user) permissions.\n");
                
                // Define scopes for MCP server
                var scopes = new[] { "https://mcp.svc.cloud.microsoft/MCP.User.Read.All", "offline_access" };
                
                // Build MSAL app with optional client secret (from Key Vault or config)
                Console.WriteLine("[Step 1] Authenticating user...");
                Console.ForegroundColor = ConsoleColor.Cyan;
                
                AuthenticationResult authResult;
                
                if (!string.IsNullOrEmpty(clientSecret))
                {
                    // Confidential client with client secret from Key Vault
                    Console.WriteLine("   Using confidential client (secret from Key Vault)");
                    
                    var confidentialApp = ConfidentialClientApplicationBuilder
                        .Create(clientId)
                        .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                        .WithClientSecret(clientSecret)
                        .WithRedirectUri("http://localhost:8400")
                        .Build();
                    
                    // Enable token caching
                    EnableTokenCache(confidentialApp.UserTokenCache);
                    
                    // Try silent auth first (from cache)
                    authResult = await TryAcquireTokenSilentOrInteractiveAsync(confidentialApp, scopes);
                }
                else
                {
                    // Public client (no secret required) - for local dev or when KV not accessible
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("   No client secret found - using public client flow");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    
                    var publicApp = PublicClientApplicationBuilder
                        .Create(clientId)
                        .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                        .WithRedirectUri("http://localhost:8400")
                        .Build();
                    
                    // Enable token caching
                    EnableTokenCache(publicApp.UserTokenCache);
                    
                    // Try silent auth first (from cache)
                    authResult = await TryAcquireTokenSilentOrInteractiveAsync(publicApp, scopes);
                }
                
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"   ✓ Authentication successful!");
                Console.WriteLine($"   ✓ User: {authResult.Account?.Username ?? "Unknown"}");
                Console.WriteLine($"   ✓ Token expires: {authResult.ExpiresOn}");
                Console.ResetColor();
                
                // Connect to MCP server with the token
                Console.WriteLine("\n[Step 2] Connecting to MCP server...");
                var mcpClient = await CreateEnterpriseMcpClientAsync(mcpServerUrl, authResult.AccessToken);
                Console.WriteLine("✓ MCP client connected to Microsoft Enterprise Server\n");
                
                // List available tools from the MCP server
                Console.WriteLine("Available MCP Tools:");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                var tools = await mcpClient.ListToolsAsync();
                foreach (var tool in tools)
                {
                    Console.WriteLine($"• {tool.Name}");
                    Console.WriteLine($"  {tool.Description}");
                    Console.WriteLine();
                }
                Console.WriteLine($"Total tools available: {tools.Count}\n");

                // Interactive conversation loop
                await RunInteractiveConversation(openAIClient, azureOpenAIEndpoint, modelDeploymentName, tools);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ Error: {ex.Message}");
                Console.WriteLine($"\nError Type: {ex.GetType().Name}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    Console.WriteLine("\n⚠️  Authentication Error:");
                    Console.WriteLine("The app registration needs Microsoft Graph API permissions.");
                    Console.WriteLine("\nRequired steps in Azure Portal:");
                    Console.WriteLine("  1. Go to Azure AD → App Registrations");
                    Console.WriteLine("  2. Select app: a68ffc23-3384-4304-b6ed-355940bd0f2a");
                    Console.WriteLine("  3. Click 'API permissions' → 'Add a permission'");
                    Console.WriteLine("  4. Select 'Microsoft Graph' → 'Application permissions'");
                    Console.WriteLine("  5. Add: User.Read.All, Directory.Read.All");
                    Console.WriteLine("  6. Click 'Grant admin consent for [tenant]'");
                    Console.WriteLine("\nFor more info: https://learn.microsoft.com/graph/mcp-server/overview");
                }
                else if (ex.Message.Contains("405") || ex.Message.Contains("Method Not Allowed"))
                {
                    Console.WriteLine("\n⚠️  The MCP endpoint may not support SSE transport.");
                    Console.WriteLine("The Microsoft MCP Server for Enterprise might require a different connection method.");
                }
                
                // Show stack trace for debugging
                Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Enables persistent token caching to a file.
        /// Tokens are cached locally so users don't need to re-authenticate every time.
        /// </summary>
        private static void EnableTokenCache(ITokenCache tokenCache)
        {
            // Ensure directory exists
            var cacheDir = Path.GetDirectoryName(TokenCacheFile);
            if (!string.IsNullOrEmpty(cacheDir) && !Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }
            
            tokenCache.SetBeforeAccess(args =>
            {
                if (File.Exists(TokenCacheFile))
                {
                    var data = File.ReadAllBytes(TokenCacheFile);
                    args.TokenCache.DeserializeMsalV3(data);
                }
            });
            
            tokenCache.SetAfterAccess(args =>
            {
                if (args.HasStateChanged)
                {
                    var data = args.TokenCache.SerializeMsalV3();
                    File.WriteAllBytes(TokenCacheFile, data);
                }
            });
        }
        
        /// <summary>
        /// Tries to acquire token silently from cache first.
        /// Falls back to interactive browser login if no cached token.
        /// </summary>
        private static async Task<AuthenticationResult> TryAcquireTokenSilentOrInteractiveAsync(
            IConfidentialClientApplication app, 
            string[] scopes)
        {
            // Try to get cached accounts
            var accounts = await app.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();
            
            if (firstAccount != null)
            {
                try
                {
                    Console.WriteLine($"   Found cached credentials for: {firstAccount.Username}");
                    Console.WriteLine("   Attempting silent authentication...");
                    
                    var result = await app.AcquireTokenSilent(scopes, firstAccount).ExecuteAsync();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("   ✓ Used cached token (no login required)");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    return result;
                }
                catch (MsalUiRequiredException)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("   Token expired, need interactive login...");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                }
            }
            else
            {
                Console.WriteLine("   No cached credentials found, need interactive login...");
            }
            
            // Fall back to authorization code flow for confidential client
            return await AcquireTokenInteractiveWithAuthCodeAsync(app, scopes);
        }
        
        /// <summary>
        /// Tries to acquire token silently from cache first.
        /// Falls back to interactive browser login if no cached token.
        /// </summary>
        private static async Task<AuthenticationResult> TryAcquireTokenSilentOrInteractiveAsync(
            IPublicClientApplication app, 
            string[] scopes)
        {
            // Try to get cached accounts
            var accounts = await app.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();
            
            if (firstAccount != null)
            {
                try
                {
                    Console.WriteLine($"   Found cached credentials for: {firstAccount.Username}");
                    Console.WriteLine("   Attempting silent authentication...");
                    
                    var result = await app.AcquireTokenSilent(scopes, firstAccount).ExecuteAsync();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("   ✓ Used cached token (no login required)");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    return result;
                }
                catch (MsalUiRequiredException)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("   Token expired, need interactive login...");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                }
            }
            else
            {
                Console.WriteLine("   No cached credentials found, need interactive login...");
            }
            
            // Fall back to interactive login for public client
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("   ╔══════════════════════════════════════════════════════════════");
            Console.WriteLine("   ║ A browser window will open for you to sign in.");
            Console.WriteLine("   ║ Please complete the authentication in the browser.");
            Console.WriteLine("   ╚══════════════════════════════════════════════════════════════");
            Console.ForegroundColor = ConsoleColor.Cyan;
            
            return await app.AcquireTokenInteractive(scopes)
                .WithUseEmbeddedWebView(false)
                .ExecuteAsync();
        }
        
        /// <summary>
        /// Interactive auth for confidential client using Authorization Code flow.
        /// Opens browser, captures auth code via local HTTP listener, exchanges for token.
        /// </summary>
        private static async Task<AuthenticationResult> AcquireTokenInteractiveWithAuthCodeAsync(
            IConfidentialClientApplication app,
            string[] scopes)
        {
            // Get authorization URL
            var authCodeUrl = await app.GetAuthorizationRequestUrl(scopes).ExecuteAsync();
            
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("   ╔══════════════════════════════════════════════════════════════");
            Console.WriteLine("   ║ A browser window will open for you to sign in.");
            Console.WriteLine("   ║ Please complete the authentication in the browser.");
            Console.WriteLine("   ╚══════════════════════════════════════════════════════════════");
            Console.ForegroundColor = ConsoleColor.Cyan;
            
            // Start local HTTP server to capture the redirect with auth code
            using var listener = new System.Net.HttpListener();
            listener.Prefixes.Add("http://localhost:8400/");
            listener.Start();
            
            // Open browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authCodeUrl.ToString(),
                UseShellExecute = true
            });
            
            // Wait for the redirect with auth code
            Console.WriteLine("   Waiting for browser authentication...");
            var context = await listener.GetContextAsync();
            var request = context.Request;
            
            // Extract auth code from query string
            var authCode = request.QueryString["code"];
            var error = request.QueryString["error"];
            var errorDescription = request.QueryString["error_description"];
            
            // Send response to browser
            var response = context.Response;
            string responseHtml;
            if (!string.IsNullOrEmpty(authCode))
            {
                responseHtml = "<html><body><h1>Authentication Successful!</h1><p>You can close this window and return to the application.</p></body></html>";
            }
            else
            {
                responseHtml = $"<html><body><h1>Authentication Failed</h1><p>Error: {error}</p><p>{errorDescription}</p></body></html>";
            }
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
            response.Close();
            listener.Stop();
            
            if (string.IsNullOrEmpty(authCode))
            {
                throw new InvalidOperationException($"Authentication failed: {error} - {errorDescription}");
            }
            
            Console.WriteLine("   ✓ Authorization code received");
            Console.WriteLine("   Exchanging authorization code for tokens...");
            
            return await app.AcquireTokenByAuthorizationCode(scopes, authCode).ExecuteAsync();
        }

        private static async Task<IMcpClient> CreateEnterpriseMcpClientAsync(
            string serverUrl, 
            string accessToken)
        {
            Console.WriteLine($"Configuring SSE transport to: {serverUrl}");
            
            // The Microsoft MCP Server for Enterprise uses SSE transport with Azure AD authentication
            var transportOptions = new SseClientTransportOptions
            {
                Endpoint = new Uri(serverUrl),
                // Add authorization header with the bearer token
                AdditionalHeaders = new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {accessToken}" }
                }
            };

            var transport = new SseClientTransport(transportOptions);
            
            Console.WriteLine("Creating MCP client...");
            return await McpClientFactory.CreateAsync(transport);
        }

        private static async Task RunInteractiveConversation(
            AzureOpenAIClient openAIClient,
            string endpoint,
            string deploymentName,
            IList<McpClientTool> tools)
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

            var chatClient = openAIClient.GetChatClient(deploymentName);
            List<ChatMessage> messages = new();
            
            // Add system message to guide the AI
            messages.Add(new SystemChatMessage(
                "You are a helpful assistant that can query Microsoft Entra tenant data using Microsoft Graph API. " +
                "You have access to tools that let you discover and execute Graph API calls. " +
                "Always use microsoft_graph_suggest_queries first to find the right API endpoint, " +
                "then use microsoft_graph_get to execute it."));
            
            // Convert MCP tools to ChatTool format
            var chatTools = new List<ChatTool>();
            
            foreach (var tool in tools)
            {
                // Try to get the tool's parameter schema
                BinaryData? parametersSchema = null;
                
                try
                {
                    // McpClientTool might have a Schema or Parameters property
                    // For now, create a generic schema that matches common MCP tool parameters
                    var schema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>(),
                        required = new List<string>()
                    };
                    
                    // Common parameter names for MS Graph MCP tools
                    if (tool.Name == "microsoft_graph_suggest_queries")
                    {
                        schema.properties["intentDescription"] = new
                        {
                            type = "string",
                            description = "The intent description or query to search for"
                        };
                        schema.required.Add("intentDescription");
                    }
                    else if (tool.Name == "microsoft_graph_get")
                    {
                        schema.properties["relativeUrl"] = new
                        {
                            type = "string",
                            description = "The relative URL for the Microsoft Graph API call"
                        };
                        schema.required.Add("relativeUrl");
                    }
                    else if (tool.Name == "microsoft_graph_list_properties")
                    {
                        schema.properties["entityName"] = new
                        {
                            type = "string",
                            description = "The entity name to list properties for"
                        };
                        schema.required.Add("entityName");
                    }
                    
                    parametersSchema = BinaryData.FromObjectAsJson(schema);
                }
                catch
                {
                    // If schema creation fails, use empty object
                    parametersSchema = BinaryData.FromString("{}");
                }
                
                // Create function definition for Azure OpenAI with proper schema
                var functionDef = ChatTool.CreateFunctionTool(
                    tool.Name,
                    tool.Description ?? "No description available",
                    parametersSchema);
                    
                chatTools.Add(functionDef);
            }

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("You: ");
                Console.ResetColor();
                
                var userInput = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(userInput))
                    continue;

                if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase) || 
                    userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("\nGoodbye!");
                    break;
                }

                messages.Add(new UserChatMessage(userInput));

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nAssistant: ");
                Console.ResetColor();

                try
                {
                    bool continueLoop = true;
                    int maxIterations = 10; // Allow more iterations for multi-step queries
                    int iteration = 0;
                    
                    while (continueLoop && iteration < maxIterations)
                    {
                        iteration++;
                        
                        // Create chat options with tools
                        var chatOptions = new ChatCompletionOptions();
                        foreach (var tool in chatTools)
                        {
                            chatOptions.Tools.Add(tool);
                        }
                        
                        var completion = await chatClient.CompleteChatAsync(messages, chatOptions);
                        var responseMessage = completion.Value;
                        
                        // Check if the model wants to call tools
                        if (responseMessage.ToolCalls.Count > 0)
                        {
                            // Add assistant message with tool calls to conversation
                            messages.Add(new AssistantChatMessage(responseMessage));
                            
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[Calling {responseMessage.ToolCalls.Count} tool(s)...]");
                            Console.ResetColor();
                            
                            // Execute each tool call
                            foreach (var toolCall in responseMessage.ToolCalls)
                            {
                                if (toolCall.Kind == ChatToolCallKind.Function)
                                {
                                    var functionCall = toolCall as ChatToolCall;
                                    var toolName = functionCall.FunctionName;
                                    var toolArgsString = functionCall.FunctionArguments.ToString();
                                    
                                    Console.WriteLine($"  → {toolName}({toolArgsString})");
                                    
                                    var mcpTool = tools.FirstOrDefault(t => t.Name == toolName);
                                    if (mcpTool != null)
                                    {
                                        try
                                        {
                                            // Parse JSON arguments to dictionary
                                            var argsDict = string.IsNullOrEmpty(toolArgsString) || toolArgsString == "{}" 
                                                ? new Dictionary<string, object?>() 
                                                : JsonSerializer.Deserialize<Dictionary<string, object?>>(toolArgsString);
                                            
                                            Console.ForegroundColor = ConsoleColor.DarkGray;
                                            Console.WriteLine($"    [DEBUG] Calling tool: {toolName}");
                                            Console.WriteLine($"    [DEBUG] Arguments: {toolArgsString}");
                                            Console.ResetColor();
                                            
                                            // Call the MCP tool
                                            var result = await mcpTool.CallAsync(argsDict);
                                            var resultJson = JsonSerializer.Serialize(result);
                                            
                                            // Check if the result contains an error from the MCP server
                                            bool hasServerError = false;
                                            string? serverErrorMessage = null;
                                            
                                            try
                                            {
                                                using var jsonDoc = JsonDocument.Parse(resultJson);
                                                var root = jsonDoc.RootElement;
                                                
                                                // Check for isError flag
                                                if (root.TryGetProperty("isError", out var isErrorProp) && 
                                                    isErrorProp.ValueKind == JsonValueKind.True)
                                                {
                                                    hasServerError = true;
                                                }
                                                
                                                // Check for error in content text
                                                if (root.TryGetProperty("content", out var contentArray) && 
                                                    contentArray.ValueKind == JsonValueKind.Array)
                                                {
                                                    foreach (var item in contentArray.EnumerateArray())
                                                    {
                                                        if (item.TryGetProperty("text", out var textProp))
                                                        {
                                                            var text = textProp.GetString() ?? "";
                                                            if (text.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                                                                text.Contains("error:", StringComparison.OrdinalIgnoreCase) ||
                                                                text.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                                                                text.Contains("No scopes found", StringComparison.OrdinalIgnoreCase) ||
                                                                text.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                                                                text.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                hasServerError = true;
                                                                serverErrorMessage = text;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            catch { /* Ignore JSON parsing errors */ }
                                            
                                            if (hasServerError)
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
                                            
                                            Console.ForegroundColor = ConsoleColor.DarkGray;
                                            Console.WriteLine($"    Result: {(resultJson.Length > 2048 ? resultJson.Substring(0, 2048) + "..." : resultJson)}");
                                            Console.ResetColor();
                                            
                                            // Add tool result to conversation
                                            messages.Add(new ToolChatMessage(toolCall.Id, resultJson));
                                        }
                                        catch (Exception toolEx)
                                        {
                                            var errorResult = new { 
                                                error = toolEx.Message,
                                                errorType = toolEx.GetType().FullName,
                                                tool = toolName,
                                                arguments = toolArgsString
                                            };
                                            messages.Add(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(errorResult)));
                                            
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine($"\n    ╔══════════════════════════════════════════════════════════════");
                                            Console.WriteLine($"    ║ TOOL ERROR DETAILS");
                                            Console.WriteLine($"    ╠══════════════════════════════════════════════════════════════");
                                            Console.WriteLine($"    ║ Tool Name:     {toolName}");
                                            Console.WriteLine($"    ║ Arguments:     {toolArgsString}");
                                            Console.WriteLine($"    ║ Error Type:    {toolEx.GetType().FullName}");
                                            Console.WriteLine($"    ║ Error Message: {toolEx.Message}");
                                            
                                            // Check for HTTP-related exceptions
                                            if (toolEx is HttpRequestException httpEx)
                                            {
                                                Console.WriteLine($"    ║ HTTP Status:   {httpEx.StatusCode}");
                                            }
                                            
                                            // Check for inner exceptions
                                            if (toolEx.InnerException != null)
                                            {
                                                Console.WriteLine($"    ║ ────────────────────────────────────────────────────────────");
                                                Console.WriteLine($"    ║ Inner Exception:");
                                                Console.WriteLine($"    ║   Type:    {toolEx.InnerException.GetType().FullName}");
                                                Console.WriteLine($"    ║   Message: {toolEx.InnerException.Message}");
                                                
                                                if (toolEx.InnerException is HttpRequestException innerHttpEx)
                                                {
                                                    Console.WriteLine($"    ║   HTTP Status: {innerHttpEx.StatusCode}");
                                                }
                                                
                                                // Check for deeper nested exceptions
                                                if (toolEx.InnerException.InnerException != null)
                                                {
                                                    Console.WriteLine($"    ║   Nested: {toolEx.InnerException.InnerException.Message}");
                                                }
                                            }
                                            
                                            // Check if error message contains useful HTTP details
                                            var errorMsg = toolEx.Message.ToLowerInvariant();
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
                                            
                                            Console.WriteLine($"    ╠══════════════════════════════════════════════════════════════");
                                            Console.WriteLine($"    ║ Stack Trace (first 5 frames):");
                                            var stackLines = toolEx.StackTrace?.Split('\n').Take(5) ?? Array.Empty<string>();
                                            foreach (var line in stackLines)
                                            {
                                                Console.WriteLine($"    ║   {line.Trim()}");
                                            }
                                            Console.WriteLine($"    ╚══════════════════════════════════════════════════════════════\n");
                                            Console.ResetColor();
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // No tool calls - we have the final response
                            continueLoop = false;
                            
                            var textContent = responseMessage.Content[0].Text;
                            Console.WriteLine(textContent);
                            Console.WriteLine();
                            
                            messages.Add(new AssistantChatMessage(responseMessage));
                        }
                    }
                    
                    if (iteration >= maxIterations)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("[Maximum iterations reached. The query may be too complex.]");
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n✗ Error during conversation: {ex.Message}\n");
                    Console.ResetColor();
                }
            }
        }
        
        /// <summary>
        /// Loads a certificate from the Windows Certificate Store by thumbprint.
        /// Searches both CurrentUser and LocalMachine stores.
        /// </summary>
        private static X509Certificate2 LoadCertificateFromStore(string thumbprint)
        {
            // Normalize thumbprint (remove spaces, convert to uppercase)
            thumbprint = thumbprint.Replace(" ", "").ToUpperInvariant();
            
            // Try CurrentUser store first
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);
                var certificates = store.Certificates.Find(
                    X509FindType.FindByThumbprint, 
                    thumbprint, 
                    validOnly: false);
                
                if (certificates.Count > 0)
                {
                    return certificates[0];
                }
            }
            
            // Try LocalMachine store
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                var certificates = store.Certificates.Find(
                    X509FindType.FindByThumbprint, 
                    thumbprint, 
                    validOnly: false);
                
                if (certificates.Count > 0)
                {
                    return certificates[0];
                }
            }
            
            throw new InvalidOperationException(
                $"Certificate with thumbprint '{thumbprint}' not found in CurrentUser or LocalMachine certificate stores.\n" +
                "To install a certificate:\n" +
                "  1. Double-click the .pfx file\n" +
                "  2. Select 'Current User' or 'Local Machine'\n" +
                "  3. Follow the wizard to import");
        }
    }
}
