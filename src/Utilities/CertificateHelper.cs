// ============================================================================
// Certificate Helper
// ============================================================================
// Loads X.509 certificates from the Windows Certificate Store for use with
// Azure AD confidential client authentication.
//
// Certificate Authentication Benefits:
//   - More secure than client secrets (no secret to leak)
//   - Can use hardware security modules (HSM)
//   - Longer validity periods
//   - Better for production environments
//
// Search Order:
//   1. CurrentUser\My store (user's personal certificates)
//   2. LocalMachine\My store (computer certificates)
// ============================================================================

using System.Security.Cryptography.X509Certificates;

namespace McpEnterpriseClient.Utilities;

/// <summary>
/// Loads certificates from the Windows Certificate Store for Azure AD authentication.
/// </summary>
/// <remarks>
/// <para>
/// Searches both CurrentUser and LocalMachine certificate stores.
/// Thumbprints are normalized (spaces removed, uppercase) before searching.
/// </para>
/// <para>
/// To install a certificate:
/// </para>
/// <code>
/// 1. Double-click the .pfx file
/// 2. Select 'Current User' or 'Local Machine'
/// 3. Follow the import wizard
/// </code>
/// </remarks>
public static class CertificateHelper
{
    /// <summary>
    /// Loads a certificate from the Windows Certificate Store by thumbprint.
    /// Searches both CurrentUser and LocalMachine stores.
    /// </summary>
    /// <param name="thumbprint">The certificate thumbprint (spaces and case are normalized).</param>
    /// <returns>The loaded <see cref="X509Certificate2"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when certificate is not found.</exception>
    public static X509Certificate2 LoadFromStore(string thumbprint)
    {
        // Normalize thumbprint (remove spaces, convert to uppercase)
        thumbprint = thumbprint.Replace(" ", "").ToUpperInvariant();

        // Try CurrentUser store first
        var cert = FindInStore(thumbprint, StoreLocation.CurrentUser);
        if (cert != null) return cert;

        // Try LocalMachine store
        cert = FindInStore(thumbprint, StoreLocation.LocalMachine);
        if (cert != null) return cert;

        throw new InvalidOperationException(
            $"Certificate with thumbprint '{thumbprint}' not found in CurrentUser or LocalMachine certificate stores.\n" +
            "To install a certificate:\n" +
            "  1. Double-click the .pfx file\n" +
            "  2. Select 'Current User' or 'Local Machine'\n" +
            "  3. Follow the wizard to import");
    }

    /// <summary>
    /// Searches a specific certificate store for a certificate by thumbprint.
    /// </summary>
    /// <param name="thumbprint">The normalized thumbprint to search for.</param>
    /// <param name="location">The store location (CurrentUser or LocalMachine).</param>
    /// <returns>The certificate if found, otherwise null.</returns>
    private static X509Certificate2? FindInStore(string thumbprint, StoreLocation location)
    {
        using var store = new X509Store(StoreName.My, location);
        store.Open(OpenFlags.ReadOnly);

        var certificates = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            thumbprint,
            validOnly: false);

        return certificates.Count > 0 ? certificates[0] : null;
    }
}
