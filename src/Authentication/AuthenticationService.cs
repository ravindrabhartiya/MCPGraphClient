using Microsoft.Identity.Client;
using System.Net;

namespace McpEnterpriseClient.Authentication;

/// <summary>
/// Handles all authentication flows for Azure AD.
/// </summary>
public class AuthenticationService
{
    private readonly TokenCacheHelper _tokenCacheHelper = new();
    private readonly string[] _scopes = { "https://mcp.svc.cloud.microsoft/MCP.User.Read.All", "offline_access" };

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

    private static void PrintBrowserPrompt()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("   ╔══════════════════════════════════════════════════════════════");
        Console.WriteLine("   ║ A browser window will open for you to sign in.");
        Console.WriteLine("   ║ Please complete the authentication in the browser.");
        Console.WriteLine("   ╚══════════════════════════════════════════════════════════════");
        Console.ForegroundColor = ConsoleColor.Cyan;
    }

    private static void PrintAuthSuccess(AuthenticationResult authResult)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"   ✓ Authentication successful!");
        Console.WriteLine($"   ✓ User: {authResult.Account?.Username ?? "Unknown"}");
        Console.WriteLine($"   ✓ Token expires: {authResult.ExpiresOn}");
        Console.ResetColor();
    }
}
