using McpEnterpriseClient.Utilities;

namespace McpEnterpriseClient.Tests.Utilities;

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
