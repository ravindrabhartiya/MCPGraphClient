# Authentication Flow: Authorization Code with Client Secret

This document describes the OAuth 2.0 Authorization Code flow with Client Secret used by the MCP Enterprise Client to authenticate with the Microsoft MCP Server for Enterprise.

## Flow Diagram

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Your App      │     │   User Browser  │     │   Azure AD      │     │   MCP Server    │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │                       │
         │ 1. Build auth URL     │                       │                       │
         │ (includes client_id,  │                       │                       │
         │  redirect_uri, scopes)│                       │                       │
         ├──────────────────────>│                       │                       │
         │                       │ 2. Open login page    │                       │
         │                       ├──────────────────────>│                       │
         │                       │                       │                       │
         │                       │ 3. User enters        │                       │
         │                       │    credentials + MFA  │                       │
         │                       │<─────────────────────>│                       │
         │                       │                       │                       │
         │                       │ 4. Redirect to        │                       │
         │                       │    localhost:8400     │                       │
         │                       │    with ?code=xxx     │                       │
         │<──────────────────────┤                       │                       │
         │                       │                       │                       │
         │ 5. Exchange code for token                    │                       │
         │    (POST with client_id + client_secret + code)                       │
         ├──────────────────────────────────────────────>│                       │
         │                                               │                       │
         │ 6. Receive access_token (delegated)           │                       │
         │<──────────────────────────────────────────────┤                       │
         │                                               │                       │
         │ 7. Call MCP Server with Bearer token                                  │
         ├──────────────────────────────────────────────────────────────────────>│
         │                                                                       │
         │ 8. MCP Server validates token, returns data                           │
         │<──────────────────────────────────────────────────────────────────────┤
```

## Step-by-Step Breakdown

| Step | Component | Action | Code Location |
|------|-----------|--------|---------------|
| **1** | App | Build authorization URL with `client_id`, `redirect_uri`, `scopes` | `GetAuthorizationRequestUrl(scopes)` |
| **2** | App | Start HTTP listener on `http://localhost:8400/` and open browser | `HttpListener` + `Process.Start` |
| **3** | Browser | User logs in to Azure AD (username, password, MFA) | Azure AD login page |
| **4** | Azure AD | Redirects to `http://localhost:8400/?code=AUTHORIZATION_CODE` | Azure AD handles this |
| **5** | App | Captures the code, sends POST to Azure AD token endpoint with `client_id` + `client_secret` + `code` | `AcquireTokenByAuthorizationCode` |
| **6** | Azure AD | Validates client secret, returns **delegated access token** with user identity | Token response |
| **7** | App | Sends request to MCP Server with `Authorization: Bearer <token>` | `CreateEnterpriseMcpClientAsync` |
| **8** | MCP Server | Validates token has correct scopes (MCP.User.Read.All, etc.), returns data | MCP Server |

## Why This Flow Works

### 1. Confidential Client
Your app registration has a client secret, making it a "confidential client" that can securely store secrets. This is different from public clients (mobile/desktop apps) that cannot securely store secrets.

### 2. Delegated Permissions
The MCP Server requires **delegated (user) permissions**, not app-only. This flow gets a token that represents the **user**, not just the app. The token contains claims about both the application and the signed-in user.

### 3. Client Secret Required
Unlike public clients, your app must prove its identity with the client secret during the token exchange (Step 5). This is why `InteractiveBrowserCredential` and `DeviceCodeCredential` from Azure.Identity failed - they don't support passing client secrets for interactive flows.

### 4. MCP Scopes
The token contains all the MCP scopes the user has consented to:
- `MCP.User.Read.All`
- `MCP.AccessReview.Read.All`
- `MCP.Policy.Read.ConditionalAccess`
- `MCP.AuditLog.Read.All`
- `MCP.GroupMember.Read.All`
- And many more...

## Configuration

| Setting | Value | Purpose |
|---------|-------|---------|
| `client_id` | `a68ffc23-3384-4304-b6ed-355940bd0f2a` | Identifies your app in Azure AD |
| `client_secret` | (in appsettings.json) | Proves app identity during token exchange |
| `tenant_id` | `73033f9b-432b-46ea-946f-7c0a6e57ac2b` | Your Azure AD tenant |
| `redirect_uri` | `http://localhost:8400` | Where Azure AD sends the authorization code |
| `scopes` | `https://mcp.svc.cloud.microsoft/MCP.User.Read.All` | What permissions to request |

## Code Implementation

### Building the Confidential Client

```csharp
var confidentialApp = ConfidentialClientApplicationBuilder
    .Create(clientId)
    .WithClientSecret(clientSecret)
    .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
    .WithRedirectUri("http://localhost:8400")
    .Build();
```

### Getting the Authorization URL

```csharp
var scopes = new[] { "https://mcp.svc.cloud.microsoft/MCP.User.Read.All", "offline_access" };
var authCodeUrl = await confidentialApp.GetAuthorizationRequestUrl(scopes)
    .ExecuteAsync();
```

### Starting Local HTTP Listener

```csharp
using var listener = new System.Net.HttpListener();
listener.Prefixes.Add("http://localhost:8400/");
listener.Start();

// Open browser for user to authenticate
System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
{
    FileName = authCodeUrl.ToString(),
    UseShellExecute = true
});

// Wait for redirect with auth code
var context = await listener.GetContextAsync();
var authCode = context.Request.QueryString["code"];
```

### Exchanging Code for Token

```csharp
var authResult = await confidentialApp.AcquireTokenByAuthorizationCode(scopes, authCode)
    .ExecuteAsync();

// authResult.AccessToken contains the delegated token
```

### Using the Token with MCP Server

```csharp
var transportOptions = new SseClientTransportOptions
{
    Endpoint = new Uri(mcpServerUrl),
    AdditionalHeaders = new Dictionary<string, string>
    {
        { "Authorization", $"Bearer {authResult.AccessToken}" }
    }
};
```

## Token Details

The resulting token is a **delegated token** that:
- Contains the user's identity (e.g., `admin@entramcpservertest01.onmicrosoft.com`)
- Contains the application's identity
- Has the MCP scopes that were consented to
- Is valid for approximately 1 hour
- Can be used to call the MCP Server on behalf of the user

## Troubleshooting

### Error: AADSTS7000218 - client_secret required
This error occurs when using `PublicClientApplication` or Azure.Identity credentials that don't pass the client secret. Solution: Use `ConfidentialClientApplicationBuilder` with `.WithClientSecret()`.

### Error: No scopes found in user token
This error occurs when using app-only authentication (ClientSecretCredential) instead of delegated authentication. The MCP Server requires delegated permissions. Solution: Use the authorization code flow with user sign-in.

### Error: Redirect URI mismatch
Ensure the redirect URI in your code matches exactly what's registered in Azure AD App Registration under "Authentication" > "Web" > "Redirect URIs".

## Azure AD App Registration Requirements

1. **Platform**: Web
2. **Redirect URI**: `http://localhost:8400`
3. **Client Secret**: Create one under "Certificates & secrets"
4. **API Permissions**: Add delegated permissions for `Microsoft MCP Server for Enterprise`
5. **Admin Consent**: Grant admin consent for the MCP permissions

## References

- [Microsoft MCP Server for Enterprise Overview](https://learn.microsoft.com/graph/mcp-server/overview)
- [OAuth 2.0 Authorization Code Flow](https://learn.microsoft.com/azure/active-directory/develop/v2-oauth2-auth-code-flow)
- [MSAL.NET Confidential Client](https://learn.microsoft.com/azure/active-directory/develop/msal-net-initializing-client-applications)
