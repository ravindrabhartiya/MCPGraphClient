// ============================================================================
// ErrorHandler Unit Tests
// ============================================================================
// Tests for the ErrorHandler utility class.
// Verifies error messages and context-specific help are displayed correctly.
// ============================================================================

using McpEnterpriseClient.Utilities;

namespace McpEnterpriseClient.Tests.Utilities;

/// <summary>
/// Unit tests for <see cref="ErrorHandler"/> class.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
/// <item>Generic exceptions display error message and type</item>
/// <item>401 errors show authentication help</item>
/// <item>405 errors show SSE transport hints</item>
/// <item>Inner exceptions are displayed</item>
/// </list>
/// </remarks>
public class ErrorHandlerTests
{
    [Fact]
    public void HandleException_WithGenericException_WritesToConsole()
    {
        // Arrange
        var exception = new Exception("Test error message");
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Act
            ErrorHandler.HandleException(exception);

            // Assert
            var output = stringWriter.ToString();
            Assert.Contains("Test error message", output);
            Assert.Contains("Exception", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void HandleException_With401Error_ShowsAuthenticationHelp()
    {
        // Arrange
        var exception = new Exception("Response status code: 401 Unauthorized");
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Act
            ErrorHandler.HandleException(exception);

            // Assert
            var output = stringWriter.ToString();
            Assert.Contains("Authentication Error", output);
            Assert.Contains("API permissions", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void HandleException_With405Error_ShowsMethodNotAllowedHelp()
    {
        // Arrange
        var exception = new Exception("Response status code: 405 Method Not Allowed");
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Act
            ErrorHandler.HandleException(exception);

            // Assert
            var output = stringWriter.ToString();
            Assert.Contains("SSE transport", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void HandleException_WithInnerException_ShowsInnerDetails()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error details");
        var exception = new Exception("Outer error", innerException);
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Act
            ErrorHandler.HandleException(exception);

            // Assert
            var output = stringWriter.ToString();
            Assert.Contains("Inner error details", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
