// ============================================================================
// Error Handler
// ============================================================================
// Provides user-friendly error messages and troubleshooting guidance.
// Detects common error patterns and prints specific remediation steps.
//
// Recognized Error Types:
//   - 401 Unauthorized: Authentication failure, token issues
//   - 405 Method Not Allowed: SSE transport or endpoint issues
//
// Error Output Includes:
//   - Error message and type
//   - Inner exception details (if present)
//   - Specific remediation steps based on error type
//   - Stack trace for debugging
// ============================================================================

namespace McpEnterpriseClient.Utilities;

/// <summary>
/// Handles exceptions and provides user-friendly error messages with
/// troubleshooting guidance.
/// </summary>
/// <remarks>
/// <para>
/// The handler analyzes exception messages to detect common issues
/// and provides specific remediation steps.
/// </para>
/// <para>
/// Example: For 401 errors, it prints instructions for configuring
/// Microsoft Graph API permissions in Azure AD.
/// </para>
/// </remarks>
public static class ErrorHandler
{
    public static void HandleException(Exception ex)
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
            PrintAuthenticationError();
        }
        else if (ex.Message.Contains("405") || ex.Message.Contains("Method Not Allowed"))
        {
            PrintMethodNotAllowedError();
        }

        Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
        Console.ResetColor();
    }

    private static void PrintAuthenticationError()
    {
        Console.WriteLine("\n⚠️  Authentication Error:");
        Console.WriteLine("The app registration needs Microsoft Graph API permissions.");
        Console.WriteLine("\nRequired steps in Azure Portal:");
        Console.WriteLine("  1. Go to Azure AD → App Registrations");
        Console.WriteLine("  2. Select your app registration");
        Console.WriteLine("  3. Click 'API permissions' → 'Add a permission'");
        Console.WriteLine("  4. Select 'Microsoft Graph' → 'Application permissions'");
        Console.WriteLine("  5. Add: User.Read.All, Directory.Read.All");
        Console.WriteLine("  6. Click 'Grant admin consent for [tenant]'");
        Console.WriteLine("\nFor more info: https://learn.microsoft.com/graph/mcp-server/overview");
    }

    private static void PrintMethodNotAllowedError()
    {
        Console.WriteLine("\n⚠️  The MCP endpoint may not support SSE transport.");
        Console.WriteLine("The Microsoft MCP Server for Enterprise might require a different connection method.");
    }
}
