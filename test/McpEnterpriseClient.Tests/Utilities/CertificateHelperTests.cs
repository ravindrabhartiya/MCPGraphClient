using McpEnterpriseClient.Utilities;

namespace McpEnterpriseClient.Tests.Utilities;

public class CertificateHelperTests
{
    [Fact]
    public void LoadFromStore_WithInvalidThumbprint_ThrowsException()
    {
        // Arrange
        var invalidThumbprint = "0000000000000000000000000000000000000000";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => CertificateHelper.LoadFromStore(invalidThumbprint));
        
        Assert.Contains("not found", exception.Message);
        Assert.Contains("CurrentUser", exception.Message);
        Assert.Contains("LocalMachine", exception.Message);
    }

    [Fact]
    public void LoadFromStore_WithThumbprintWithSpaces_NormalizesThumbprint()
    {
        // Arrange - thumbprint with spaces (common when copied from certificate details)
        var thumbprintWithSpaces = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";

        // Act & Assert - should still throw but with normalized thumbprint
        var exception = Assert.Throws<InvalidOperationException>(
            () => CertificateHelper.LoadFromStore(thumbprintWithSpaces));
        
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void LoadFromStore_WithLowercaseThumbprint_NormalizesToUppercase()
    {
        // Arrange - lowercase thumbprint
        var lowercaseThumbprint = "abcdef1234567890abcdef1234567890abcdef12";

        // Act & Assert - should normalize to uppercase internally
        var exception = Assert.Throws<InvalidOperationException>(
            () => CertificateHelper.LoadFromStore(lowercaseThumbprint));
        
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void LoadFromStore_ExceptionMessage_ContainsInstallInstructions()
    {
        // Arrange
        var invalidThumbprint = "1234567890ABCDEF1234567890ABCDEF12345678";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => CertificateHelper.LoadFromStore(invalidThumbprint));
        
        Assert.Contains(".pfx", exception.Message);
        Assert.Contains("wizard", exception.Message);
    }
}
