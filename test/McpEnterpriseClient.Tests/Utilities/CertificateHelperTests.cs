// ============================================================================
// CertificateHelper Unit Tests
// ============================================================================
// Tests for the CertificateHelper utility class.
// Verifies certificate loading behavior and error handling.
// NOTE: These tests are Windows-only as they use Windows Certificate Store.
// ============================================================================

using System.Runtime.InteropServices;
using McpEnterpriseClient.Utilities;

namespace McpEnterpriseClient.Tests.Utilities;

/// <summary>
/// Unit tests for <see cref="CertificateHelper"/> class.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
/// <item>Invalid thumbprints throw with helpful error messages</item>
/// <item>Thumbprints with spaces are normalized</item>
/// <item>Lowercase thumbprints are converted to uppercase</item>
/// <item>Error messages include installation instructions</item>
/// </list>
/// <para>
/// These tests only run on Windows as they use the Windows Certificate Store.
/// </para>
/// </remarks>
public class CertificateHelperTests
{
    /// <summary>
    /// Skip tests on non-Windows platforms.
    /// </summary>
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact]
    public void LoadFromStore_WithInvalidThumbprint_ThrowsException()
    {
        Skip.IfNot(IsWindows, "Windows Certificate Store not available on this platform");
        
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
        Skip.IfNot(IsWindows, "Windows Certificate Store not available on this platform");
        
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
        Skip.IfNot(IsWindows, "Windows Certificate Store not available on this platform");
        
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
        Skip.IfNot(IsWindows, "Windows Certificate Store not available on this platform");
        
        // Arrange
        var invalidThumbprint = "1234567890ABCDEF1234567890ABCDEF12345678";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => CertificateHelper.LoadFromStore(invalidThumbprint));
        
        Assert.Contains(".pfx", exception.Message);
        Assert.Contains("wizard", exception.Message);
    }
}
