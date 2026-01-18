# Microsoft MCP Server for Enterprise - C# Client

A C# console application that connects to Microsoft's MCP (Model Context Protocol) Server for Enterprise, enabling AI-powered queries against your Microsoft 365 tenant data using Microsoft Graph API.

## ✅ What's Working

- ✅ Connects to Microsoft MCP Server at `https://mcp.svc.cloud.microsoft/enterprise`
- ✅ Authenticates using Azure AD app registration with client credentials
- ✅ Discovers and uses 3 MCP tools:
  - `microsoft_graph_suggest_queries` - Discovers the right Graph API endpoints
  - `microsoft_graph_get` - Executes Graph API calls
  - `microsoft_graph_list_properties` - Explores Graph entity schemas
- ✅ Interactive AI chat that automatically calls MCP tools
- ✅ Tool calling workflow: AI determines which tools to call based on your questions

## Architecture

```
User Question → Azure OpenAI (gpt-4o) → MCP Tools → Microsoft Graph API → Your M365 Tenant Data
```

The application uses:
- **Azure OpenAI**: `gpt-4o` model for natural language understanding and tool orchestration
- **MCP Protocol**: Connects to Microsoft's enterprise MCP server via SSE (Server-Sent Events) transport
- **Azure AD Authentication**: Service principal (app registration) with client secret credentials
- **Tool Calling**: AI automatically invokes MCP tools to answer your questions

## Prerequisites

- .NET 8.0 SDK or later
- Azure subscription
- Azure OpenAI resource with `gpt-4o` model deployed
- Azure AD app registration (service principal)
- Microsoft 365 tenant (for querying data)

## Setup Instructions

### 1. Azure OpenAI Resource

Create or use an existing Azure OpenAI resource:
1. Go to [Azure Portal](https://portal.azure.com)
2. Create/select an Azure OpenAI resource
3. Deploy the `gpt-4o` model
4. Get the API key from "Keys and Endpoint"

### 2. Azure AD App Registration

Create an app registration for MCP authentication:

1. Go to **Azure Active Directory** → **App registrations** → **New registration**
2. Name: `MCP Enterprise Client`
3. Supported account types: **Accounts in this organizational directory only**
4. Click **Register**

#### Configure API Permissions

⚠️ **CRITICAL**: The app needs Microsoft Graph permissions to query tenant data:

1. Go to your app → **API permissions**
2. Click **Add a permission** → **Microsoft Graph** → **Application permissions**
3. Add these permissions:
   - `User.Read.All` - Read all users' full profiles
   - `Directory.Read.All` - Read directory data
   - `Group.Read.All` - Read all groups (optional, for group queries)
   - `AuditLog.Read.All` - Read audit logs (optional, for sign-in activity)

4. **Click "Grant admin consent for [Your Tenant]"** ← This step is REQUIRED!

#### Create Client Secret

1. Go to **Certificates & secrets** → **New client secret**
2. Description: `MCP Client Secret`
3. Expires: Choose duration (recommend 24 months)
4. Click **Add**
5. **Copy the secret value immediately** (you can't see it again)

### 3. Configure Application

The app supports multiple authentication methods (in priority order):

| Method | Best For | Config |
|--------|----------|--------|
| Managed Identity | Azure deployments | `UseManagedIdentity=true` |
| Certificate (Store) | On-premises/Windows | `CertificateThumbprint` |
| Certificate (File) | Containers/Linux | `CertificatePath` |
| Client Secret | Development | `ClientSecret` or env var |
| Public Client | Development | No credentials (if enabled in Azure AD) |

#### Option A: Environment Variable (Recommended for Local Dev)

Keep secrets out of config files by using environment variables:

```powershell
# PowerShell - set for current session
$env:AZURE_CLIENT_SECRET = "your-secret-value"
dotnet run --project McpEnterpriseClient.csproj
```

```bash
# Bash/Linux/WSL
export AZURE_CLIENT_SECRET="your-secret-value"
dotnet run --project McpEnterpriseClient.csproj
```

#### Option B: Configuration File

Edit `appsettings.json`:

```json
{
  "AzureAD": {
    "TenantId": "YOUR-TENANT-ID",
    "ClientId": "YOUR-CLIENT-ID",
    "ClientSecret": "YOUR-CLIENT-SECRET"  // Or use CertificateThumbprint
  },
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com",
    "DeploymentName": "gpt-4o",
    "ApiKey": "YOUR-OPENAI-API-KEY"
  },
  "McpServer": {
    "Endpoint": "https://mcp.svc.cloud.microsoft/enterprise"
  }
}
```

#### Option C: Public Client Flow (Requires Azure AD Config)

If no credentials are configured, the app uses public client flow. This requires:

1. Go to Azure Portal → Azure AD → App registrations → Your app
2. Go to **Authentication** blade
3. Under "Advanced settings", set **Allow public client flows** to **Yes**
4. Click Save

⚠️ This is less secure than using client credentials.

## Running the Application

```bash
cd "c:\Users\ravibh\MCP Client"
dotnet run --project McpEnterpriseClient.csproj
```

## Example Queries

Once running, you can ask questions like:

- "How many users are in my tenant?"
- "List all guest users"
- "Show me users who didn't sign in last month"
- "Is MFA enabled for all administrators?"
- "What groups does [user] belong to?"

The AI will:
1. Call `microsoft_graph_suggest_queries` to find the right API endpoint
2. Call `microsoft_graph_get` to execute the query
3. Present the results in natural language

## Troubleshooting

### "No scopes found in user token" Error

**Cause**: The app registration doesn't have Microsoft Graph API permissions or admin consent wasn't granted.

**Solution**:
1. Go to Azure Portal → Azure AD → App registrations
2. Select your app (Client ID: a68ffc23-3384-4304-b6ed-355940bd0f2a)
3. Go to **API permissions**
4. Ensure `User.Read.All` and `Directory.Read.All` are added
5. **Click "Grant admin consent for [tenant]"** - this is critical!
6. Wait 5-10 minutes for permissions to propagate
7. Try running the application again

### "An error occurred invoking microsoft_graph_suggest_queries" Error

**Cause**: The parameter schema might not match what the MCP server expects.

**Current Status**: The application now correctly passes `intentDescription`, `relativeUrl`, and `entityName` parameters based on the tool being called.

### Connection Issues

- Ensure your app registration has valid credentials
- Check that the client secret hasn't expired
- Verify the tenant ID is correct
- Make sure you have internet connectivity to `mcp.svc.cloud.microsoft`

## Authentication Flow

```
1. App Registration → Client Credentials Flow
2. Acquire Token (scope: https://mcp.svc.cloud.microsoft/.default)
3. Create SSE Transport with Bearer Token
4. Connect to MCP Server
5. Discover Available Tools
6. User asks question → AI calls MCP tools → MCP Server calls Graph API with delegated permissions
```

## Key Files

- `Program.cs` - Main application logic, MCP client creation, chat loop
- `appsettings.json` - Configuration (credentials, endpoints)
- `McpEnterpriseClient.csproj` - Project file with NuGet dependencies

## Dependencies

- `ModelContextProtocol` v0.3.0-preview.4 - MCP SDK
- `Azure.AI.OpenAI` v2.0.0 - Azure OpenAI client
- `Azure.Identity` v1.13.1 - Azure AD authentication
- `Microsoft.Extensions.Configuration` - Configuration management

## Learn More

- [Microsoft MCP Server Overview](https://learn.microsoft.com/graph/mcp-server/overview)
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Microsoft Graph API](https://learn.microsoft.com/graph/)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/ai-services/openai/)

## Status

**Current Version**: 1.0 - Tool calling fully functional, requires Graph API permissions for data access
