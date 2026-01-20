// ============================================================================
// Authentication Service
// ============================================================================
// Handles Azure AD authentication for the MCP client. Supports multiple
// authentication flows:
//
//   1. Confidential Client (with client secret)
//      - Uses client credentials + authorization code flow
//      - Recommended for server applications
//
//   2. Public Client (no secret)
//      - Uses interactive browser authentication
//      - Falls back when no credentials configured
//
// Token Caching:
//   - Tokens are cached locally to avoid repeated logins
//   - Silent authentication is attempted first
//   - Interactive login only when cache miss or token expired
// ============================================================================

using Microsoft.Identity.Client;
using System.Net;

namespace McpEnterpriseClient.Authentication;

/// <summary>
/// Handles Azure AD authentication with support for multiple credential types.
/// </summary>
/// <remarks>
/// <para>
/// The service uses MSAL (Microsoft Authentication Library) and supports:
/// </para>
/// <list type="bullet">
/// <item>Silent token acquisition from cache</item>
/// <item>Interactive browser-based login</item>
/// <item>Authorization code flow with local HTTP listener</item>
/// </list>
/// <para>
/// Tokens are requested with the MCP service scope:
/// <c>https://mcp.svc.cloud.microsoft/MCP.User.Read.All</c>
/// </para>
/// </remarks>
public class AuthenticationService
{
    private readonly TokenCacheHelper _tokenCacheHelper = new();
    private readonly string[] _scopes = { "https://mcp.svc.cloud.microsoft/MCP.User.Read.All", "offline_access" };

    /// <summary>
    /// Authenticates a user with Azure AD and returns an access token.
    /// </summary>
    /// <param name="tenantId">The Azure AD tenant ID.</param>
    /// <param name="clientId">The application (client) ID from app registration.</param>
    /// <param name="clientSecret">The client secret (null for public client flow).</param>
    /// <returns>The authentication result containing the access token.</returns>
    /// <remarks>
    /// Uses confidential client flow if secret is provided, otherwise public client flow.
    /// Attempts silent authentication first using cached tokens.
    /// </remarks>
    public async Task<AuthenticationResult> AuthenticateAsync(
        string tenantId,
        string clientId,
        string? clientSecret)
    {
        Console.WriteLine("[Step 1] Authenticating user...");
        Console.ForegroundColor = ConsoleColor.Cyan;

        AuthenticationResult authResult;

        if (!string.IsNullOrEmpty(clientSecret))
        {
            authResult = await AuthenticateWithConfidentialClientAsync(tenantId, clientId, clientSecret);
        }
        else
        {
            authResult = await AuthenticateWithPublicClientAsync(tenantId, clientId);
        }

        Console.ResetColor();
        PrintAuthSuccess(authResult);

        return authResult;
    }

    /// <summary>
    /// Authenticates using confidential client (with client secret).
    /// </summary>
    /// <param name="tenantId">The Azure AD tenant ID.</param>
    /// <param name="clientId">The application (client) ID.</param>
    /// <param name="clientSecret">The client secret.</param>
    /// <returns>The authentication result.</returns>
    private async Task<AuthenticationResult> AuthenticateWithConfidentialClientAsync(
        string tenantId,
        string clientId,
        string clientSecret)
    {
        Console.WriteLine("   Using confidential client (secret from Key Vault)");

        var app = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            .WithClientSecret(clientSecret)
            .WithRedirectUri("http://localhost:8400")
            .Build();

        _tokenCacheHelper.EnableTokenCache(app.UserTokenCache);

        return await TryAcquireTokenSilentOrInteractiveAsync(app);
    }

    /// <summary>
    /// Authenticates using public client (interactive browser login).
    /// </summary>
    /// <param name="tenantId">The Azure AD tenant ID.</param>
    /// <param name="clientId">The application (client) ID.</param>
    /// <returns>The authentication result.</returns>
    /// <remarks>
    /// Used when no client secret is configured. Opens a browser for user login.
    /// </remarks>
    private async Task<AuthenticationResult> AuthenticateWithPublicClientAsync(
        string tenantId,
        string clientId)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("   No client secret found - using public client flow");
        Console.ForegroundColor = ConsoleColor.Cyan;

        var app = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            .WithRedirectUri("http://localhost:8400")
            .Build();

        _tokenCacheHelper.EnableTokenCache(app.UserTokenCache);

        return await TryAcquireTokenSilentOrInteractiveAsync(app);
    }

    /// <summary>
    /// Attempts silent token acquisition, falling back to interactive auth code flow.
    /// </summary>
    /// <param name="app">The confidential client application.</param>
    /// <returns>The authentication result.</returns>
    private async Task<AuthenticationResult> TryAcquireTokenSilentOrInteractiveAsync(
        IConfidentialClientApplication app)
    {
        var accounts = await app.GetAccountsAsync();
        var firstAccount = accounts.FirstOrDefault();

        if (firstAccount != null)
        {
            try
            {
                Console.WriteLine($"   Found cached credentials for: {firstAccount.Username}");
                Console.WriteLine("   Attempting silent authentication...");

                var result = await app.AcquireTokenSilent(_scopes, firstAccount).ExecuteAsync();
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

        return await AcquireTokenInteractiveWithAuthCodeAsync(app);
    }

    /// <summary>
    /// Attempts silent token acquisition, falling back to interactive browser login.
    /// </summary>
    /// <param name="app">The public client application.</param>
    /// <returns>The authentication result.</returns>
    private async Task<AuthenticationResult> TryAcquireTokenSilentOrInteractiveAsync(
        IPublicClientApplication app)
    {
        var accounts = await app.GetAccountsAsync();
        var firstAccount = accounts.FirstOrDefault();

        if (firstAccount != null)
        {
            try
            {
                Console.WriteLine($"   Found cached credentials for: {firstAccount.Username}");
                Console.WriteLine("   Attempting silent authentication...");

                var result = await app.AcquireTokenSilent(_scopes, firstAccount).ExecuteAsync();
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

        PrintBrowserPrompt();

        return await app.AcquireTokenInteractive(_scopes)
            .WithUseEmbeddedWebView(false)
            .ExecuteAsync();
    }

    /// <summary>
    /// Acquires a token using authorization code flow with a local HTTP listener.
    /// </summary>
    /// <param name="app">The confidential client application.</param>
    /// <returns>The authentication result.</returns>
    /// <remarks>
    /// Opens a browser for user login, listens on localhost:8400 for the callback,
    /// then exchanges the authorization code for tokens.
    /// </remarks>
    private async Task<AuthenticationResult> AcquireTokenInteractiveWithAuthCodeAsync(
        IConfidentialClientApplication app)
    {
        var authCodeUrl = await app.GetAuthorizationRequestUrl(_scopes).ExecuteAsync();

        PrintBrowserPrompt();

        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8400/");
        listener.Start();

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = authCodeUrl.ToString(),
            UseShellExecute = true
        });

        Console.WriteLine("   Waiting for browser authentication...");
        var context = await listener.GetContextAsync();
        var request = context.Request;

        var authCode = request.QueryString["code"];
        var error = request.QueryString["error"];
        var errorDescription = request.QueryString["error_description"];

        await SendBrowserResponse(context, authCode, error, errorDescription);
        listener.Stop();

        if (string.IsNullOrEmpty(authCode))
        {
            throw new InvalidOperationException($"Authentication failed: {error} - {errorDescription}");
        }

        Console.WriteLine("   ✓ Authorization code received");
        Console.WriteLine("   Exchanging authorization code for tokens...");

        return await app.AcquireTokenByAuthorizationCode(_scopes, authCode).ExecuteAsync();
    }

    /// <summary>
    /// Sends an HTML response to the browser after authentication redirect.
    /// </summary>
    /// <param name="context">The HTTP listener context.</param>
    /// <param name="authCode">The authorization code (null if auth failed).</param>
    /// <param name="error">The error code (if auth failed).</param>
    /// <param name="errorDescription">The error description (if auth failed).</param>
    private static async Task SendBrowserResponse(
        HttpListenerContext context,
        string? authCode,
        string? error,
        string? errorDescription)
    {
        var response = context.Response;
        string responseHtml = !string.IsNullOrEmpty(authCode)
            ? "<html><body><h1>Authentication Successful!</h1><p>You can close this window and return to the application.</p></body></html>"
            : $"<html><body><h1>Authentication Failed</h1><p>Error: {error}</p><p>{errorDescription}</p></body></html>";

        var buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    /// <summary>
    /// Prints a visual prompt indicating browser authentication is required.
    /// </summary>
    private static void PrintBrowserPrompt()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("   ╔══════════════════════════════════════════════════════════════");
        Console.WriteLine("   ║ A browser window will open for you to sign in.");
        Console.WriteLine("   ║ Please complete the authentication in the browser.");
        Console.WriteLine("   ╚══════════════════════════════════════════════════════════════");
        Console.ForegroundColor = ConsoleColor.Cyan;
    }

    /// <summary>
    /// Prints authentication success details including username and token expiry.
    /// </summary>
    /// <param name="authResult">The authentication result to display.</param>
    private static void PrintAuthSuccess(AuthenticationResult authResult)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"   ✓ Authentication successful!");
        Console.WriteLine($"   ✓ User: {authResult.Account?.Username ?? "Unknown"}");
        Console.WriteLine($"   ✓ Token expires: {authResult.ExpiresOn}");
        Console.ResetColor();
    }
}
