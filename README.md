# Microsoft MCP Server for Enterprise - C# Client

A C# client application for connecting to Microsoft's MCP (Model Context Protocol) Server for Enterprise. This client enables you to query enterprise data in your Microsoft Entra tenant using natural language through AI-powered interactions.

## Overview

The Microsoft MCP Server for Enterprise (`https://mcp.svc.cloud.microsoft/enterprise`) is a programmatic interface for AI agents to query enterprise data by translating natural language requests into Microsoft Graph API calls.

This C# client:
- ✅ Connects to Microsoft's MCP Server for Enterprise
- ✅ Integrates with Azure OpenAI for AI-powered conversations
- ✅ Uses Azure Identity for secure authentication
- ✅ Provides an interactive CLI for querying your tenant data
- ✅ Leverages the official MCP C# SDK

## Features

### MCP Tools Available

The Microsoft MCP Server for Enterprise exposes three powerful tools:

1. **microsoft_graph_suggest_queries** - Uses RAG to search Microsoft Graph API examples aligned with your intent
2. **microsoft_graph_get** - Executes read-only Microsoft Graph API calls with proper permissions
3. **microsoft_graph_list_properties** - Retrieves schema for Microsoft Graph entities

### Example Use Cases

- 📊 **IT Helpdesk**: "Which users didn't sign in last month?"
- 🔐 **Security**: "Is MFA enabled for all administrators?"
- 👥 **User Management**: "How many guest users do we have?"
- 📝 **Reporting**: "Show me all unassigned licenses in my tenant"
- 🔍 **Discovery**: "List all inactive user accounts with Copilot licenses"

## Prerequisites

1. **.NET 8.0 SDK** or later
2. **Azure OpenAI** resource with a deployed model (e.g., gpt-4o)
3. **Azure Active Directory** credentials with appropriate permissions
4. **Microsoft Entra tenant** access
5. **Appropriate licenses** for the data you're accessing

## Setup

### 1. Configure Azure OpenAI

Update `appsettings.json` with your Azure OpenAI details:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-AZURE-OPENAI-ENDPOINT.openai.azure.com",
    "DeploymentName": "gpt-4o"
  },
  "McpServer": {
    "Endpoint": "https://mcp.svc.cloud.microsoft/enterprise"
  }
}
```

Or set environment variables:

```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://YOUR-ENDPOINT.openai.azure.com"
$env:AZURE_OPENAI_DEPLOYMENT_NAME = "gpt-4o"
```

### 2. Authentication

The client uses `DefaultAzureCredential` which supports multiple authentication methods:

- **Visual Studio**: Sign in through Tools → Azure Service Authentication
- **Azure CLI**: Run `az login`
- **Visual Studio Code**: Install Azure Account extension and sign in
- **Environment Variables**: Set `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET`
- **Managed Identity**: When running in Azure

### 3. Install Dependencies

```bash
dotnet restore
```

## Running the Client

### Build and Run

```bash
dotnet build
dotnet run
```

### Interactive Mode

Once running, you can ask natural language questions:

```
You: How many users do we have in our tenant?
Assistant: There are 10,930 users in the directory.

You: List all users who didn't sign in last month
Assistant: [Lists users with details...]

You: Show me all guest users
Assistant: [Shows guest user information...]
```

Type `exit` or `quit` to end the session.

## Architecture

```
┌─────────────────────┐
│   C# MCP Client     │
│  (This Application) │
└──────────┬──────────┘
           │
           ├─────────────────────┐
           │                     │
           ▼                     ▼
┌──────────────────┐   ┌─────────────────────────┐
│  Azure OpenAI    │   │  Microsoft MCP Server   │
│   (GPT-4o)       │   │    for Enterprise       │
└──────────────────┘   └───────────┬─────────────┘
                                   │
                                   ▼
                        ┌─────────────────────┐
                        │  Microsoft Graph    │
                        │       APIs          │
                        └─────────────────────┘
```

## How It Works

1. **User Query**: You ask a natural language question
2. **AI Processing**: Azure OpenAI analyzes the query
3. **Tool Selection**: AI decides which MCP tools to use
4. **Query Suggestion**: `microsoft_graph_suggest_queries` finds relevant Graph API patterns
5. **Execution**: `microsoft_graph_get` executes the Graph API call
6. **Response**: Results are converted back to natural language

## Security & Permissions

- All operations respect Microsoft Graph permissions
- User privileges and tenant security policies are enforced
- Only **read-only** operations are supported
- Rate limit: 100 calls per minute per user
- Subject to standard Microsoft Graph throttling limits

## Licensing

- ✅ No additional cost for MCP Server for Enterprise
- ⚠️ Requires appropriate licenses for accessed data
- ⚠️ May need Microsoft Entra ID P2 for some features

## Current Limitations (Preview)

- 🔍 **Scope**: Read-only access to Microsoft Entra identity and directory data
- 🌍 **Availability**: Public cloud only (not available in sovereign clouds yet)
- ⏱️ **Rate Limits**: 100 calls/minute per user
- 🏢 **Focus**: User, group, application, and device insights

## Troubleshooting

### Authentication Issues

```
Error: Unable to authenticate
```

**Solution**: Ensure you're logged in via Azure CLI or Visual Studio:
```bash
az login
```

### Configuration Issues

```
Error: Azure OpenAI endpoint not configured
```

**Solution**: Set the endpoint in `appsettings.json` or environment variables.

### Permission Issues

```
Error: Insufficient privileges
```

**Solution**: Ensure your Azure AD account has appropriate Graph API permissions.

## Monitoring and Logs

Enable Microsoft Graph activity logs in your tenant to monitor MCP usage:

```kusto
MicrosoftGraphActivityLogs
| where TimeGenerated >= ago(30d)
| where AppId == "e8c77dc2-69b3-43f4-bc51-3213c9d915b4"
| project RequestId, TimeGenerated, UserId, RequestMethod, RequestUri, ResponseStatusCode
```

## Project Structure

```
MCP Client/
├── Program.cs                    # Main application logic
├── McpEnterpriseClient.csproj   # Project file
├── appsettings.json             # Configuration
├── .gitignore                   # Git ignore rules
└── README.md                    # This file
```

## Dependencies

- `ModelContextProtocol` (v0.3.0-preview.4) - MCP C# SDK
- `Azure.Identity` - Azure authentication
- `Azure.AI.OpenAI` - Azure OpenAI client
- `Microsoft.Extensions.AI` - AI abstractions and extensions
- `Microsoft.Extensions.Configuration` - Configuration management

## Resources

- [Microsoft MCP Server for Enterprise Documentation](https://learn.microsoft.com/en-us/graph/mcp-server/overview)
- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- [MCP C# SDK Documentation](https://learn.microsoft.com/en-us/dotnet/ai/get-started-mcp)
- [Microsoft Graph API Reference](https://learn.microsoft.com/en-us/graph/api/overview)

## License

This project is provided as-is for demonstration purposes. The Microsoft MCP Server for Enterprise is offered under the [Microsoft APIs Terms of Use](https://learn.microsoft.com/en-us/legal/microsoft-apis/terms-of-use).

## Contributing

Feel free to submit issues and enhancement requests!

---

**Note**: Microsoft MCP Server for Enterprise is currently in PREVIEW and may change substantially before general availability.
