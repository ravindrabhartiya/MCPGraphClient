# Building an AI-Powered Microsoft Entra Investigation Tool with MCP Server

**How to use Microsoft's MCP Server for Enterprise to query your Entra tenant with natural language**

---

## Introduction

Imagine asking your computer: *"How many users in our tenant haven't signed in for 30 days?"* and getting an instant, accurate answer. No need to remember complex Microsoft Graph API queries or navigate through multiple Azure portal blades.

This is now possible with **Microsoft's MCP Server for Enterprise** – a Model Context Protocol server that exposes Microsoft Graph API capabilities to AI agents. In this post, I'll walk you through building a C# client that connects to this server, enabling natural language queries against your Microsoft Entra tenant.

## What You'll Build

By the end of this guide, you'll have:
- A console application that authenticates users and connects to Microsoft's MCP Server
- Integration with Azure OpenAI for natural language understanding
- The ability to query your Entra tenant using plain English
- A foundation for building automated Entra investigation agents

```
┌─────────────────────────────────────────────────────────────────┐
│  User: "How many groups don't have owners?"                     │
│                           ↓                                      │
│  Azure OpenAI (GPT-4o) interprets the question                  │
│                           ↓                                      │
│  MCP Server suggests: /v1.0/groups?$filter=owners/$count eq 0   │
│                           ↓                                      │
│  Microsoft Graph executes the query                              │
│                           ↓                                      │
│  Response: "There are 399 groups without owners"                │
└─────────────────────────────────────────────────────────────────┘
```

## Prerequisites

Before we start, you'll need:

1. **Azure Subscription** with:
   - Azure OpenAI Service (with GPT-4o deployed)
   - Azure Key Vault (for secure secret storage)

2. **Microsoft Entra ID** (formerly Azure AD):
   - An app registration with MCP Server permissions
   - Admin consent for the MCP delegated permissions

3. **Development Environment**:
   - .NET 8.0 SDK
   - Visual Studio Code or Visual Studio
   - Azure CLI (for local development)

## Step 1: Create the App Registration

First, register an application in Microsoft Entra ID:

1. Go to [Azure Portal](https://portal.azure.com) → **Microsoft Entra ID** → **App registrations**
2. Click **New registration**
3. Name: `MCP Entra Client`
4. Supported account types: **Single tenant**
5. Redirect URI: **Web** → `http://localhost:8400`
6. Click **Register**

### Add API Permissions

1. Go to **API permissions** → **Add a permission**
2. Select **APIs my organization uses**
3. Search for **Microsoft MCP Server for Enterprise**
4. Select **Delegated permissions** and add:
   - `MCP.User.Read.All`
   - `MCP.AccessReview.Read.All`
   - `MCP.AuditLog.Read.All`
   - `MCP.GroupMember.Read.All`
   - (Add others as needed)
5. Click **Grant admin consent**

### Create a Client Secret

1. Go to **Certificates & secrets** → **New client secret**
2. Description: `MCP Client Secret`
3. Expiry: Choose appropriate duration
4. **Copy the secret value immediately** – you won't see it again!

## Step 2: Set Up Azure Key Vault

Store your secrets securely in Azure Key Vault:

```powershell
# Create Key Vault
az keyvault create --name your-mcp-vault --resource-group your-rg --location westus2

# Add secrets (use -- for nested config keys)
az keyvault secret set --vault-name your-mcp-vault --name "AzureAD--TenantId" --value "your-tenant-id"
az keyvault secret set --vault-name your-mcp-vault --name "AzureAD--ClientId" --value "your-client-id"
az keyvault secret set --vault-name your-mcp-vault --name "AzureAD--ClientSecret" --value "your-secret"
az keyvault secret set --vault-name your-mcp-vault --name "AzureOpenAI--Endpoint" --value "https://your-openai.openai.azure.com"
az keyvault secret set --vault-name your-mcp-vault --name "AzureOpenAI--ApiKey" --value "your-api-key"

# Grant yourself access
az keyvault set-policy --name your-mcp-vault --upn your-email@domain.com --secret-permissions get list
```

## Step 3: Create the .NET Project

```powershell
# Create new console app
dotnet new console -n McpEntraClient
cd McpEntraClient

# Add required packages
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Identity.Client
dotnet add package ModelContextProtocol --prerelease
```

## Step 4: Configure the Application

Create `appsettings.json`:

```json
{
  "KeyVault": {
    "Uri": "https://your-mcp-vault.vault.azure.net/"
  },
  "AzureAD": {
    "TenantId": "",
    "ClientId": "",
    "ClientSecret": ""
  },
  "AzureOpenAI": {
    "Endpoint": "",
    "DeploymentName": "gpt-4o",
    "ApiKey": ""
  },
  "McpServer": {
    "Endpoint": "https://mcp.svc.cloud.microsoft/enterprise"
  }
}
```

All sensitive values are loaded from Key Vault at runtime – the empty strings are just placeholders.

## Step 5: Understanding the Authentication Flow

The MCP Server requires **delegated (user) permissions**, not app-only. This means:

1. A user must sign in interactively
2. The token represents both the app AND the signed-in user
3. The user's permissions determine what data can be accessed

```
┌─────────────────────────────────────────────────────────────────┐
│  1. App builds authorization URL                                │
│  2. Browser opens → User logs in with MFA                       │
│  3. Azure AD redirects to localhost:8400 with auth code         │
│  4. App exchanges code + client secret for tokens               │
│  5. App calls MCP Server with Bearer token                      │
└─────────────────────────────────────────────────────────────────┘
```

### Token Caching

To avoid requiring login every time, implement token caching:

```csharp
private static readonly string TokenCacheFile = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "McpEntraClient", "msal_token_cache.bin");

private static void EnableTokenCache(ITokenCache tokenCache)
{
    tokenCache.SetBeforeAccess(args =>
    {
        if (File.Exists(TokenCacheFile))
            args.TokenCache.DeserializeMsalV3(File.ReadAllBytes(TokenCacheFile));
    });
    
    tokenCache.SetAfterAccess(args =>
    {
        if (args.HasStateChanged)
            File.WriteAllBytes(TokenCacheFile, args.TokenCache.SerializeMsalV3());
    });
}
```

## Step 6: Connect to MCP Server

The MCP Server uses SSE (Server-Sent Events) transport:

```csharp
var transportOptions = new SseClientTransportOptions
{
    Endpoint = new Uri("https://mcp.svc.cloud.microsoft/enterprise"),
    AdditionalHeaders = new Dictionary<string, string>
    {
        { "Authorization", $"Bearer {accessToken}" }
    }
};

var transport = new SseClientTransport(transportOptions);
var mcpClient = await McpClientFactory.CreateAsync(transport);

// List available tools
var tools = await mcpClient.ListToolsAsync();
```

The MCP Server exposes three tools:
- `microsoft_graph_suggest_queries` – Discovers the right API endpoint for your intent
- `microsoft_graph_get` – Executes Graph API calls
- `microsoft_graph_list_properties` – Explores entity schemas

## Step 7: Build the AI Agent Loop

The magic happens when you combine Azure OpenAI with MCP tools:

```csharp
var chatClient = openAIClient.GetChatClient("gpt-4o");
var messages = new List<ChatMessage>();

messages.Add(new SystemChatMessage(
    "You are a helpful assistant that can query Microsoft Entra tenant data. " +
    "Always use microsoft_graph_suggest_queries first to find the right endpoint, " +
    "then use microsoft_graph_get to execute it."));

// When user asks a question
messages.Add(new UserChatMessage(userInput));

// AI decides which tools to call
var completion = await chatClient.CompleteChatAsync(messages, chatOptions);

// If tool calls are requested, execute them and loop back
foreach (var toolCall in completion.Value.ToolCalls)
{
    var result = await mcpTool.CallAsync(arguments);
    messages.Add(new ToolChatMessage(toolCall.Id, resultJson));
}
```

## Real-World Investigation Scenarios

### Scenario 1: Finding Stale Accounts

```
You: "Show me all users who haven't signed in for 90 days"

AI: [Calling microsoft_graph_suggest_queries...]
    [Calling microsoft_graph_get with filter on signInActivity...]
    
Response: "Found 47 users who haven't signed in for 90+ days:
- user1@contoso.com (last sign-in: 2025-10-15)
- user2@contoso.com (last sign-in: 2025-09-28)
..."
```

### Scenario 2: Security Audit

```
You: "Are there any groups without owners?"

AI: [Calling microsoft_graph_suggest_queries...]
    [Calling /v1.0/groups?$filter=owners/$count eq 0...]
    
Response: "There are 399 groups without owners. Here are the first 20:
- TestGroup183
- testgroup141
..."
```

### Scenario 3: Conditional Access Review

```
You: "List all conditional access policies that don't require MFA"

AI: [Calling microsoft_graph_suggest_queries...]
    [Calling /beta/identity/conditionalAccess/policies...]
    
Response: "Found 3 policies without MFA requirement:
- Legacy App Access
- Guest User Policy
..."
```

## Building Automated Agents

Once you have the basic client working, you can evolve it into fully automated agents. The patterns shown here can be extended using the **Microsoft Agent Framework** (also known as Azure AI Agent Service) for production-grade agent deployments.

### Why Build Agents?

Manual querying is great for ad-hoc investigations, but many scenarios benefit from automation:

| Use Case | Manual | Automated Agent |
|----------|--------|-----------------|
| Daily stale account check | ❌ Tedious | ✅ Scheduled job |
| Compliance reporting | ❌ Time-consuming | ✅ Weekly email reports |
| Security incident response | ❌ Slow | ✅ Real-time alerts |
| Onboarding audits | ❌ Easy to forget | ✅ Triggered workflows |

### Agent Architecture Patterns

There are several ways to structure your Entra agents:

**Pattern 1: Simple Scheduled Agent**
```
Timer Trigger → Query MCP Server → Generate Report → Send Email/Teams
```

**Pattern 2: Event-Driven Agent**
```
Azure Event Grid (Entra events) → Process Event → Query Context → Take Action
```

**Pattern 3: Conversational Agent (with Microsoft Agent Framework)**
```
User Message → Agent Framework → Orchestrate Tools → MCP Server → Response
```

### Using Microsoft Agent Framework

The [Microsoft Agent Framework](https://learn.microsoft.com/azure/ai-services/agents/) provides enterprise-grade capabilities for building AI agents:

- **Tool orchestration** – Automatically select and chain tools
- **Memory and context** – Maintain conversation state across sessions
- **Tracing and observability** – Debug agent behavior with built-in telemetry
- **Multi-agent patterns** – Coordinate multiple specialized agents

Here's how you might integrate MCP tools with the Agent Framework:

```csharp
using Microsoft.Extensions.AI;

// Register MCP tools with the Agent Framework
var agentBuilder = new AgentBuilder()
    .WithModel("gpt-4o")
    .WithSystemPrompt("You are an Entra security analyst...")
    .WithTools(mcpTools);  // MCP tools from the server

// The agent automatically orchestrates tool calls
var agent = agentBuilder.Build();
var response = await agent.RunAsync("Check for risky sign-ins today");
```

### Example: Daily Stale Account Report

```csharp
public class StaleAccountAgent
{
    private readonly IMcpClient _mcpClient;
    private readonly int _staleDays = 90;
    
    public async Task<List<StaleUser>> GetStaleAccountsAsync()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-_staleDays).ToString("yyyy-MM-ddTHH:mm:ssZ");
        
        var result = await _mcpClient.CallToolAsync("microsoft_graph_get", new
        {
            relativeUrl = $"/v1.0/users?$filter=signInActivity/lastSignInDateTime le {cutoffDate}&$select=displayName,userPrincipalName,signInActivity"
        });
        
        return ParseUsers(result);
    }
    
    public async Task RunDailyReportAsync()
    {
        var staleUsers = await GetStaleAccountsAsync();
        
        if (staleUsers.Count > 0)
        {
            var report = GenerateHtmlReport(staleUsers);
            await SendEmailAsync("security-team@contoso.com", "Daily Stale Account Report", report);
            
            // Optionally create tickets for follow-up
            foreach (var user in staleUsers.Where(u => u.DaysSinceLastSignIn > 180))
            {
                await CreateServiceNowTicketAsync($"Review stale account: {user.UserPrincipalName}");
            }
        }
    }
}
```

### Example: Group Governance Agent

This agent runs on a schedule and identifies governance issues across all groups:

```csharp
public class GroupGovernanceAgent
{
    private readonly IMcpClient _mcpClient;
    private readonly ILogger _logger;
    
    public async Task<GroupHealthReport> AnalyzeGroupHealthAsync()
    {
        var report = new GroupHealthReport();
        
        // Groups without owners - security risk!
        report.GroupsWithoutOwners = await GetGroupsWithoutOwnersAsync();
        _logger.LogInformation($"Found {report.GroupsWithoutOwners.Count} groups without owners");
        
        // Groups without members - cleanup candidates
        report.EmptyGroups = await GetEmptyGroupsAsync();
        
        // Stale groups (no activity in 180 days)
        report.StaleGroups = await GetStaleGroupsAsync();
        
        // Groups with excessive members (potential oversharing)
        report.LargeGroups = await GetGroupsWithMemberCountOver(1000);
        
        return report;
    }
    
    public async Task RemediateAsync(GroupHealthReport report)
    {
        // Auto-assign IT as owner for orphaned groups
        foreach (var group in report.GroupsWithoutOwners)
        {
            await AssignDefaultOwnerAsync(group.Id, "it-admins@contoso.com");
            await CreateAuditLogAsync($"Auto-assigned owner to group: {group.DisplayName}");
        }
        
        // Send notifications for groups requiring human review
        await NotifySecurityTeamAsync(report.LargeGroups, "Groups with excessive membership");
    }
}
```

### Example: Security Incident Response Agent

For real-time security monitoring, combine MCP with Azure Event Grid:

```csharp
public class SecurityIncidentAgent
{
    // Triggered when Identity Protection detects a risky sign-in
    public async Task HandleRiskySignInAsync(RiskySignInEvent signInEvent)
    {
        // Get more context about the user
        var userDetails = await _mcpClient.CallToolAsync("microsoft_graph_get", new
        {
            relativeUrl = $"/v1.0/users/{signInEvent.UserId}?$select=displayName,department,jobTitle,manager"
        });
        
        // Check recent activity
        var recentSignIns = await GetRecentSignInsAsync(signInEvent.UserId, hours: 24);
        
        // Use AI to analyze the pattern
        var analysis = await _aiClient.AnalyzeAsync($@"
            User {userDetails.DisplayName} ({userDetails.JobTitle}) had a risky sign-in.
            Location: {signInEvent.Location}
            Risk Level: {signInEvent.RiskLevel}
            Recent sign-ins: {recentSignIns.Count} in last 24 hours from {recentSignIns.DistinctLocations} locations.
            
            Is this likely a true positive or false positive? What actions should we take?
        ");
        
        // Take automated action based on risk
        if (analysis.RecommendedAction == "Block")
        {
            await BlockUserSignInsAsync(signInEvent.UserId);
            await NotifySecurityTeamAsync(signInEvent, analysis, Priority.High);
        }
    }
}
```

### Multi-Agent Orchestration

For complex scenarios, you can orchestrate multiple specialized agents:

```csharp
// Coordinator agent that delegates to specialists
public class EntraSecurityCoordinator
{
    private readonly StaleAccountAgent _staleAccountAgent;
    private readonly GroupGovernanceAgent _groupAgent;
    private readonly SecurityIncidentAgent _securityAgent;
    
    public async Task RunDailySecurityReviewAsync()
    {
        var tasks = new List<Task<AgentReport>>
        {
            _staleAccountAgent.RunAsync(),
            _groupAgent.RunAsync(),
            _securityAgent.GetDailySummaryAsync()
        };
        
        var reports = await Task.WhenAll(tasks);
        
        // Aggregate findings
        var executiveSummary = await _aiClient.SummarizeAsync(reports);
        
        // Post to Teams security channel
        await _teamsClient.PostAdaptiveCardAsync("Security-Alerts", executiveSummary);
    }
}
```

## Security Considerations

1. **Never store secrets in code** – Use Azure Key Vault
2. **Use least-privilege permissions** – Only request the MCP scopes you need
3. **Implement proper token caching** – But protect the cache file
4. **Audit access** – Log who queries what data
5. **Use Managed Identity in production** – Eliminates secrets entirely

## Deploying to Azure

For production deployment:

1. **Azure Container Apps** or **Azure App Service**
2. **Managed Identity** for Key Vault access
3. **Application Insights** for monitoring
4. **Azure Monitor** for alerting

```yaml
# Example: Container Apps with Managed Identity
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    configuration: {
      secrets: [
        {
          name: 'keyvault-uri'
          value: keyVaultUri
        }
      ]
    }
  }
}
```

## Conclusion

Microsoft's MCP Server for Enterprise opens up powerful possibilities for:

- **Security teams** investigating incidents
  - *"Show me all failed sign-in attempts from outside the US in the last 24 hours"*
  - *"Which users have risky sign-ins flagged by Identity Protection?"*
  - *"List all service principals with expiring credentials"*

- **IT admins** auditing their tenant
  - *"How many guest users do we have and when were they last active?"*
  - *"Show me all groups without owners or with only one owner"*
  - *"Which users have direct license assignments instead of group-based?"*

- **Developers** building governance automation
  - *"Find all app registrations with secrets expiring in the next 30 days"*
  - *"List custom roles and their assignments"*
  - *"Show me all dynamic groups and their membership rules"*

- **Compliance officers** generating reports
  - *"Which admin accounts don't have MFA enabled?"*
  - *"List all users with privileged role assignments"*
  - *"Show me audit logs for directory role changes this month"*

The combination of natural language understanding (Azure OpenAI) and structured API access (MCP Server) makes complex Graph API queries accessible to everyone – no need to memorize OData syntax or navigate API documentation.

### What's Next?

Once you're comfortable with the basics, consider:

1. **Building scheduled agents** that run daily health checks
2. **Creating alert workflows** that notify on policy violations  
3. **Generating automated reports** for compliance requirements
4. **Integrating with ticketing systems** for automated remediation

## Resources

- [Microsoft MCP Server Documentation](https://learn.microsoft.com/graph/mcp-server/overview)
- [Microsoft Graph API Reference](https://learn.microsoft.com/graph/api/overview)
- [Model Context Protocol Specification](https://modelcontextprotocol.io)
- [Source Code for this Project](https://github.com/ravindrabhartiya/MCPGraphClient)

---

*Have questions or feedback? Feel free to open an issue on the GitHub repository!*
